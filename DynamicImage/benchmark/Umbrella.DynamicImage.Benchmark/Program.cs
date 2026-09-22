using BenchmarkDotNet.Running;
using Umbrella.DynamicImage.Benchmark;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

try
{
	if (args.FirstOrDefault() == "--generate-fixtures")
	{
		FixtureGenerator.Generate(args.Length == 2 ? args[1] : throw new ArgumentException("Supply an output directory."));
	}
	else if (args.FirstOrDefault() == "--memory-child")
	{
		await ProcessMemoryRunner.RunChildAsync(args[1], args[2]);
	}
	else if (args.FirstOrDefault() == "--process-memory")
	{
		return await ProcessMemoryRunner.RunAsync(args.Skip(1).ToArray());
	}
	else
	{
		var config = DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(60));
		var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
		return summaries.Any(x => x.HasCriticalValidationErrors || x.Reports.Any(r => !r.Success)) ? 1 : 0;
	}

	return 0;
}
catch (Exception exception)
{
	await Console.Error.WriteLineAsync(exception.ToString());
	return 1;
}
