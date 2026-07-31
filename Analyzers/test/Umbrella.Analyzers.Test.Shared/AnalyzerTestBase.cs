using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers.Test.Shared;

/// <summary>
/// Base test class for analyzer tests with common setup and helper methods.
/// </summary>
public abstract class AnalyzerTestBase<T>
	where T : DiagnosticAnalyzer, new()
{
	private static readonly FrozenSet<string> _rulePrefixes =
	[
		"UMS",  // Umbrella Analyzer - Model Standards
		"UA",   // Umbrella Analyzer
		"UDA",  // Umbrella DataAccess Analyzer
		"UMA",  // Umbrella Mapperly Analyzer
		"UWDI"  // Umbrella WebUtilities DynamicImage Analyzer
	];

	/// <summary>
	/// Creates an expected diagnostic result for the specified rule at the given location.
	/// </summary>
	/// <param name="rule">The diagnostic descriptor rule.</param>
	/// <param name="line">The line number (1-based).</param>
	/// <param name="column">The column number (1-based).</param>
	/// <param name="arguments">Optional arguments for the diagnostic message format.</param>
	/// <returns>An expected diagnostic result for testing.</returns>
	protected static ExpectedDiagnostic Diagnostic(DiagnosticDescriptor rule, int line, int column, params object[] arguments)
	{
		return new ExpectedDiagnostic(rule, line, column, arguments);
	}

	/// <summary>
	/// Runs the analyzer on the provided source code and verifies the expected diagnostics.
	/// </summary>
	/// <param name="source">The source code to analyze.</param>
	/// <param name="expected">The expected diagnostic results.</param>
	/// <returns>A task representing the asynchronous verification operation.</returns>
	protected static async Task VerifyAnalyzerAsync(string source, params ExpectedDiagnostic[] expected)
	{
		ArgumentNullException.ThrowIfNull(expected);

		CSharpCompilation compilation = CreateCompilation(source);
		var analyzer = new T();

		CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);
		ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

		// Filter out compiler diagnostics, only keep analyzer diagnostics
		Diagnostic[] analyzerDiagnostics = [.. diagnostics.Where(d => _rulePrefixes.Any(x => d.Id.StartsWith(x, StringComparison.Ordinal)))];

		VerifyDiagnostics(analyzerDiagnostics, expected);
	}

	/// <summary>
	/// Runs the analyzer on the provided source code with additional metadata references and verifies the expected
	/// diagnostics.
	/// </summary>
	/// <param name="source">The source code to analyze.</param>
	/// <param name="additionalReferences">The additional metadata references to include in the compilation.</param>
	/// <param name="expected">The expected diagnostic results.</param>
	/// <returns>A task representing the asynchronous verification operation.</returns>
	protected static async Task VerifyAnalyzerAsync(
		string source,
		IEnumerable<MetadataReference> additionalReferences,
		params ExpectedDiagnostic[] expected)
	{
		ArgumentNullException.ThrowIfNull(additionalReferences);
		ArgumentNullException.ThrowIfNull(expected);

		CSharpCompilation compilation = CreateCompilation(source, additionalReferences);
		var analyzer = new T();

		CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);
		ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

		Diagnostic[] analyzerDiagnostics = [.. diagnostics.Where(d => _rulePrefixes.Any(x => d.Id.StartsWith(x, StringComparison.Ordinal)))];

		VerifyDiagnostics(analyzerDiagnostics, expected);
	}

	/// <summary>
	/// Runs the analyzer on the provided source code and verifies no diagnostics are reported.
	/// </summary>
	/// <param name="source">The source code to analyze.</param>
	/// <returns>A task representing the asynchronous verification operation.</returns>
	protected static async Task VerifyNoDiagnosticsAsync(string source)
	{
		await VerifyAnalyzerAsync(source);
	}

	/// <summary>
	/// Runs the analyzer with additional non-C# source files.
	/// </summary>
	protected static async Task VerifyAnalyzerWithAdditionalFilesAsync(
		string source,
		IEnumerable<(string Path, string Text)> additionalFiles,
		params ExpectedDiagnostic[] expected)
	{
		ArgumentNullException.ThrowIfNull(additionalFiles);
		ArgumentNullException.ThrowIfNull(expected);

		CSharpCompilation compilation = CreateCompilation(source);
		var analyzer = new T();
		ImmutableArray<AdditionalText> texts =
		[
			.. additionalFiles.Select(x => (AdditionalText)new InMemoryAdditionalText(x.Path, x.Text))
		];
		var analyzerOptions = new AnalyzerOptions(texts);
		CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers([analyzer], analyzerOptions);
		ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
		Diagnostic[] analyzerDiagnostics = [.. diagnostics.Where(d => _rulePrefixes.Any(x => d.Id.StartsWith(x, StringComparison.Ordinal)))];

		VerifyDiagnostics(analyzerDiagnostics, expected);
	}

	/// <summary>
	/// Runs the analyzer on the provided source code with additional metadata references and verifies no diagnostics are
	/// reported.
	/// </summary>
	/// <param name="source">The source code to analyze.</param>
	/// <param name="additionalReferences">The additional metadata references to include in the compilation.</param>
	/// <returns>A task representing the asynchronous verification operation.</returns>
	protected static async Task VerifyNoDiagnosticsAsync(string source, IEnumerable<MetadataReference> additionalReferences)
	{
		await VerifyAnalyzerAsync(source, additionalReferences);
	}

	/// <summary>
	/// Creates a compilation from the provided source code.
	/// </summary>
	/// <param name="source">The source code to compile.</param>
	/// <param name="additionalReferences">Optional additional metadata references to include in the compilation.</param>
	/// <returns>A compilation object.</returns>
	private static CSharpCompilation CreateCompilation(string source, IEnumerable<MetadataReference>? additionalReferences = null)
	{
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

		string[] referencePaths =
		[
			.. (((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? []),
			typeof(Diagnostic).Assembly.Location,
			typeof(CSharpCompilation).Assembly.Location,
			typeof(T).Assembly.Location
		];

		MetadataReference[] references =
		[
			.. referencePaths
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(x => MetadataReference.CreateFromFile(x))
		];

		MetadataReference[] allReferences = additionalReferences is null
			? references
			: [.. references.Concat(additionalReferences)];

		return CSharpCompilation.Create(
			assemblyName: "TestAssembly",
			syntaxTrees: new[] { syntaxTree },
			references: allReferences,
			options: new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary,
				nullableContextOptions: NullableContextOptions.Enable));
	}

	/// <summary>
	/// Verifies that the actual diagnostics match the expected diagnostics.
	/// </summary>
	/// <param name="actualDiagnostics">The actual diagnostics from the analyzer.</param>
	/// <param name="expectedDiagnostics">The expected diagnostics.</param>
	private static void VerifyDiagnostics(Diagnostic[] actualDiagnostics, ExpectedDiagnostic[] expectedDiagnostics)
	{
		if (expectedDiagnostics.Length != actualDiagnostics.Length)
		{
			Console.WriteLine($"Expected {expectedDiagnostics.Length} diagnostics, but got {actualDiagnostics.Length}");
			Console.WriteLine("Actual diagnostics:");

			foreach (var diagnostic in actualDiagnostics)
			{
				var lineSpan = diagnostic.Location.GetLineSpan();
				Console.WriteLine($"  {diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)} at line {lineSpan.StartLinePosition.Line + 1}, column {lineSpan.StartLinePosition.Character + 1}");
			}

			Console.WriteLine("Expected diagnostics:");

			foreach (var expected in expectedDiagnostics)
			{
				Console.WriteLine($"  {expected.Rule.Id}: line {expected.Line}, column {expected.Column}");
			}
		}

		Assert.Equal(expectedDiagnostics.Length, actualDiagnostics.Length);

		var unmatchedActual = actualDiagnostics.ToList();

		foreach (var expected in expectedDiagnostics)
		{
			var partialMatch = unmatchedActual.FirstOrDefault(actual => actual.Id == expected.Rule.Id && (expected.Arguments.Count == 0 || string.Format(CultureInfo.InvariantCulture, expected.Rule.MessageFormat.ToString(CultureInfo.InvariantCulture), expected.Arguments.ToArray()) == actual.GetMessage(CultureInfo.InvariantCulture)));

			var match = unmatchedActual.FirstOrDefault(actual =>
				actual.Id == expected.Rule.Id &&
				(expected.Arguments.Count == 0 || string.Format(CultureInfo.InvariantCulture, expected.Rule.MessageFormat.ToString(CultureInfo.InvariantCulture), expected.Arguments.ToArray()) == actual.GetMessage(CultureInfo.InvariantCulture)) &&
				actual.Location.GetLineSpan().StartLinePosition.Line + 1 == expected.Line &&
				actual.Location.GetLineSpan().StartLinePosition.Character + 1 == expected.Column
			);

			if (match == null)
			{
				string partialMatchMessage = partialMatch is not null
					? $" but was found on line {partialMatch.Location.GetLineSpan().StartLinePosition.Line + 1}, column {partialMatch.Location.GetLineSpan().StartLinePosition.Character + 1}."
					: ".";

				throw new Xunit.Sdk.XunitException($"Expected diagnostic {expected.Rule.Id} at line {expected.Line}, column {expected.Column} not found{partialMatchMessage}");
			}

			_ = unmatchedActual.Remove(match);
		}

		if (unmatchedActual.Count > 0)
			throw new Xunit.Sdk.XunitException($"Unexpected diagnostics found: {string.Join(", ", unmatchedActual.Select(d => d.Id))}");
	}

	/// <summary>
	/// Debug version of VerifyAnalyzerAsync for troubleshooting.
	/// </summary>
	/// <param name="source">The source code to analyze.</param>
	/// <returns>A task representing the asynchronous verification operation.</returns>
	protected static async Task DebugAnalyzerAsync(string source)
	{
		CSharpCompilation compilation = CreateCompilation(source);
		var analyzer = new T();

		CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
		ImmutableArray<Diagnostic> allDiagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
		ImmutableArray<Diagnostic> analyzerDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

		// Debug output
		Console.WriteLine($"Total all diagnostics: {allDiagnostics.Length}");
		Console.WriteLine($"Total analyzer diagnostics: {analyzerDiagnostics.Length}");

		Console.WriteLine("\nAll diagnostics:");
		foreach (var diagnostic in allDiagnostics)
		{
			var lineSpan = diagnostic.Location.GetLineSpan();
			Console.WriteLine($"  {diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)} at line {lineSpan.StartLinePosition.Line + 1}, column {lineSpan.StartLinePosition.Character + 1}");
		}

		Console.WriteLine("\nAnalyzer diagnostics:");
		foreach (var diagnostic in analyzerDiagnostics)
		{
			var lineSpan = diagnostic.Location.GetLineSpan();
			Console.WriteLine($"  {diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)} at line {lineSpan.StartLinePosition.Line + 1}, column {lineSpan.StartLinePosition.Character + 1}");
		}
	}

	private sealed class InMemoryAdditionalText : AdditionalText
	{
		private readonly Microsoft.CodeAnalysis.Text.SourceText _text;
		public override string Path { get; }

		public InMemoryAdditionalText(string path, string text)
		{
			Path = path;
			_text = Microsoft.CodeAnalysis.Text.SourceText.From(text);
		}

		public override Microsoft.CodeAnalysis.Text.SourceText GetText(CancellationToken cancellationToken = default) => _text;
	}
}

/// <summary>
/// Represents an expected diagnostic result for testing purposes.
/// </summary>
public sealed class ExpectedDiagnostic
{
	public DiagnosticDescriptor Rule { get; }
	public int Line { get; }
	public int Column { get; }
	public IReadOnlyList<object> Arguments { get; }

	public ExpectedDiagnostic(DiagnosticDescriptor rule, int line, int column, params object[] arguments)
	{
		Rule = rule;
		Line = line;
		Column = column;
		Arguments = arguments ?? [];
	}
}
