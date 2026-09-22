using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.DynamicImage.Benchmark;

internal static class ProcessMemoryRunner
{
	private const string Ready = "MEMORY_READY";
	private const string Done = "MEMORY_DONE:";
	private const int Iterations = 20;
	private static readonly string[] _implementations = ["FreeImage", "NetVips", "SkiaSharp"];

	public static async Task RunChildAsync(string implementation, string scenarioId)
	{
		ResizerWorkload workload = new(implementation, ResizeScenario.Find(scenarioId));
		int outputBytes = workload.Validate(); // First of three warm-ups; validates with this backend only.
		for (int i = 0; i < 2; i++)
			_ = Consume(workload);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		Console.WriteLine(Ready);
		if (await Console.In.ReadLineAsync() != "GO")
			throw new InvalidOperationException("Missing parent start signal.");
		long checksum = 0;
		for (int i = 0; i < Iterations; i++)
			checksum += Consume(workload);
		Console.WriteLine($"{Done}{outputBytes}:{checksum}");
		if (await Console.In.ReadLineAsync() != "EXIT")
			throw new InvalidOperationException("Missing parent completion signal.");
		GC.KeepAlive(workload);
	}

	// Keep the returned array local to one call, including the final iteration.
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static int Consume(ResizerWorkload workload)
	{
		var result = workload.Resize();
		return result.resizedBytes.Length + result.resizedBytes[0];
	}

	public static async Task<int> RunAsync(string[] args)
	{
		string scenarioPattern = "*", implementationPattern = "*";
		string output = Path.Combine("BenchmarkDotNet.Artifacts", "process-memory", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
		int repetitions = 3;
		double timeoutSeconds = 600;
		for (int i = 0; i < args.Length; i += 2)
		{
			if (i + 1 == args.Length)
				throw new ArgumentException($"Missing value for {args[i]}.");
			switch (args[i])
			{
				case "--scenario": scenarioPattern = args[i + 1]; break;
				case "--implementation": implementationPattern = args[i + 1]; break;
				case "--output": output = args[i + 1]; break;
				case "--repetitions": repetitions = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
				case "--timeout-seconds": timeoutSeconds = double.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
				default: throw new ArgumentException($"Unknown memory option: {args[i]}");
			}
		}

		if (repetitions < 1 || !double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0)
			throw new ArgumentException("Repetitions and timeout must be positive.");
		var cases = (from scenario in ResizeScenario.All
					 from implementation in _implementations
					 where (scenario.Options.Format != DynamicImageFormat.Avif || implementation == "NetVips")
					 && Matches(scenario.Id, scenarioPattern) && Matches(implementation, implementationPattern)
					 select (scenario, implementation)).ToArray();
		if (cases.Length == 0)
			throw new ArgumentException("No supported cases match the supplied filters.");
		_ = Directory.CreateDirectory(output);
		List<MemoryResult> results = [];
		foreach (var (scenario, implementation) in cases)
		{
			for (int repetition = 1; repetition <= repetitions; repetition++)
			{
				Console.WriteLine($"Memory: {implementation}/{scenario.Id}, repetition {repetition}/{repetitions}");
				results.Add(await MeasureAsync(implementation, scenario.Id, repetition, TimeSpan.FromSeconds(timeoutSeconds), output));
				WriteReports(output, results);
			}
		}

		Console.WriteLine($"Process memory reports: {Path.GetFullPath(output)}");
		return results.Any(x => x.Status != "OK") ? 1 : 0;
	}

	private static bool Matches(string value, string pattern) => Regex.IsMatch(value,
		"^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static async Task<MemoryResult> MeasureAsync(string implementation, string scenario, int repetition, TimeSpan timeout, string directory)
	{
		MemoryResult result = new(implementation, scenario, repetition);
		using CancellationTokenSource cancellation = new(timeout);
		CancellationToken token = cancellation.Token;
		ProcessStartInfo start = new(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate executable."))
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		if (string.Equals(Path.GetFileNameWithoutExtension(start.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
			start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
		start.ArgumentList.Add("--memory-child");
		start.ArgumentList.Add(implementation);
		start.ArgumentList.Add(scenario);
		using Process process = new() { StartInfo = start };
		StringBuilder log = new();
		Task<string>? errors = null;
		bool started = false;
		try
		{
			started = process.Start();
			if (!started)
				throw new InvalidOperationException("Failed to start memory worker.");
			result.ProcessId = process.Id;
			errors = process.StandardError.ReadToEndAsync();
			_ = await ReadMarkerAsync(process.StandardOutput, Ready, log, token);
			process.Refresh();
			result.BaselineWorkingSet = process.WorkingSet64;
			result.BaselinePrivateBytes = process.PrivateMemorySize64;
			result.MaxWorkingSet = result.BaselineWorkingSet;
			result.MaxPrivateBytes = result.BaselinePrivateBytes;
			await process.StandardInput.WriteLineAsync("GO".AsMemory(), token);
			await process.StandardInput.FlushAsync(token);
			Task<string> completion = ReadMarkerAsync(process.StandardOutput, Done, log, token);
			while (!completion.IsCompleted)
			{
				Sample(process, result);
				_ = await Task.WhenAny(completion, Task.Delay(10, token));
				token.ThrowIfCancellationRequested();
			}

			string message = await completion;
			Sample(process, result);
			result.OutputBytes = int.Parse(message[Done.Length..].Split(':')[0], CultureInfo.InvariantCulture);
			result.EndWorkingSet = process.WorkingSet64;
			result.EndPrivateBytes = process.PrivateMemorySize64;
			result.LifetimePeakWorkingSet = process.PeakWorkingSet64;
			await process.StandardInput.WriteLineAsync("EXIT".AsMemory(), token);
			await process.StandardInput.FlushAsync(token);
			await process.WaitForExitAsync(token);
			if (process.ExitCode != 0)
				throw new InvalidOperationException($"Worker exit code: {process.ExitCode}");
			result.Status = "OK";
		}
		catch (Exception exception)
		{
			result.Status = exception is OperationCanceledException ? "Timeout" : "Failed";
			result.Error = exception.Message;
			await Console.Error.WriteLineAsync($"{result.Status}: {implementation}/{scenario}: {exception.Message}");
		}
		finally
		{
			if (started && !process.HasExited)
			{
				process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync();
			}

			if (errors is not null)
				_ = log.AppendLine(await errors);
			await File.WriteAllTextAsync(Path.Combine(directory, $"{implementation}-{scenario}-{repetition}.log"), log.ToString());
		}

		return result;
	}

	private static void Sample(Process process, MemoryResult result)
	{
		process.Refresh();
		result.MaxWorkingSet = Math.Max(result.MaxWorkingSet, process.WorkingSet64);
		result.MaxPrivateBytes = Math.Max(result.MaxPrivateBytes, process.PrivateMemorySize64);
		result.Samples++;
	}

	private static async Task<string> ReadMarkerAsync(StreamReader reader, string marker, StringBuilder log, CancellationToken token)
	{
		while (await reader.ReadLineAsync(token) is { } line)
		{
			_ = log.AppendLine(line);
			if (line.StartsWith(marker, StringComparison.Ordinal))
				return line;
		}

		throw new InvalidOperationException($"Worker exited before {marker}; see its log for native-library or encoder errors.");
	}

	private static void WriteReports(string directory, List<MemoryResult> results)
	{
		string[] labels = ["BaselineWorkingSet", "MaxWorkingSet", "EndWorkingSet", "BaselinePrivateBytes", "MaxPrivateBytes", "EndPrivateBytes", "LifetimePeakWorkingSet", "OutputBytes"];
		string header = "Implementation,Scenario,Repetition,ProcessId,Status,Samples," + string.Join(',', labels) + ",Error";
		File.WriteAllLines(Path.Combine(directory, "raw.csv"), new[] { header }.Concat(results.Select(r =>
			$"{r.Implementation},{r.Scenario},{r.Repetition},{r.ProcessId},{r.Status},{r.Samples}," +
			string.Join(',', r.Values.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "," + Csv(r.Error))));
		List<string> csv = ["Implementation,Scenario,SuccessfulRuns,FailedRuns," + string.Join(',', labels.Select(x => "Median" + x))];
		List<string> markdown =
		[
			"# Process memory footprint", "",
			$"{RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; {RuntimeInformation.FrameworkDescription}; logical CPUs: {Environment.ProcessorCount}",
			$"Three warm-ups; {Iterations} sequential resizes per run; 10 ms sampling target. All memory values are bytes. Native threading defaults are unchanged; NetVips concurrency is recorded in worker logs.",
			"These are process footprints (managed + native + runtime), not allocations per operation. Sampled maxima may miss short peaks; lifetime peak includes startup and warm-up. Medians include successful runs only.", "",
			"| Implementation | Scenario | Successful / failed | " + string.Join(" | ", labels.Select(x => "Median " + x)) + " |",
			"|---|---|---|" + string.Join('|', labels.Select(_ => "---:")) + "|"
		];
		foreach (var group in results.GroupBy(r => (r.Implementation, r.Scenario)))
		{
			var successful = group.Where(r => r.Status == "OK").ToArray();
			string[] medians = Enumerable.Range(0, labels.Length).Select(i => successful.Length == 0 ? "" :
				Median(successful.Select(r => r.Values[i])).ToString(CultureInfo.InvariantCulture)).ToArray();
			int failures = group.Count() - successful.Length;
			csv.Add($"{group.Key.Implementation},{group.Key.Scenario},{successful.Length},{failures}," + string.Join(',', medians));
			markdown.Add($"| {group.Key.Implementation} | {group.Key.Scenario} | {successful.Length} / {failures} | " + string.Join(" | ", medians) + " |");
		}

		File.WriteAllLines(Path.Combine(directory, "summary.csv"), csv);
		File.WriteAllLines(Path.Combine(directory, "summary.md"), markdown);
		File.WriteAllLines(Path.Combine(directory, "raw.md"), new[] { "# Raw process memory results", "", "```csv", header }
			.Concat(results.Select(r => $"{r.Implementation},{r.Scenario},{r.Repetition},{r.ProcessId},{r.Status},{r.Samples}," + string.Join(',', r.Values) + "," + Csv(r.Error)))
			.Append("```"));
	}

	private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
	private static double Median(IEnumerable<long> values)
	{
		long[] ordered = values.Order().ToArray();
		return (ordered[(ordered.Length - 1) / 2] / 2.0) + (ordered[ordered.Length / 2] / 2.0);
	}

	private sealed class MemoryResult(string implementation, string scenario, int repetition)
	{
		public string Implementation { get; } = implementation;
		public string Scenario { get; } = scenario;
		public int Repetition { get; } = repetition;
		public int ProcessId { get; set; }
		public string Status { get; set; } = "Failed";
		public string Error { get; set; } = "";
		public int Samples { get; set; }
		public long BaselineWorkingSet { get; set; }
		public long MaxWorkingSet { get; set; }
		public long EndWorkingSet { get; set; }
		public long BaselinePrivateBytes { get; set; }
		public long MaxPrivateBytes { get; set; }
		public long EndPrivateBytes { get; set; }
		public long LifetimePeakWorkingSet { get; set; }
		public int OutputBytes { get; set; }
		public long[] Values => [BaselineWorkingSet, MaxWorkingSet, EndWorkingSet, BaselinePrivateBytes, MaxPrivateBytes, EndPrivateBytes, LifetimePeakWorkingSet, OutputBytes];
	}
}