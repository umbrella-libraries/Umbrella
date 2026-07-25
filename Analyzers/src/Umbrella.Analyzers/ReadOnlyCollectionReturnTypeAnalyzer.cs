using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks if collection payloads returned by changeable public methods use read-only collection types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReadOnlyCollectionReturnTypeAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA006";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Method return types should use read-only collection types",
		"Method '{0}' should return a read-only collection type instead of {1}",
		"CodeStyle",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static compilationContext =>
		{
			var methodAnalysis = new ChangeablePublicMethodAnalysis(compilationContext.Compilation);
			var collectionAnalysis = new CollectionTypeAnalysis(compilationContext.Compilation);
			compilationContext.RegisterSymbolAction(
				context => AnalyzeMethod(context, methodAnalysis, collectionAnalysis),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(
		SymbolAnalysisContext context,
		ChangeablePublicMethodAnalysis methodAnalysis,
		CollectionTypeAnalysis collectionAnalysis)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (!methodAnalysis.IsEligible(methodSymbol))
			return;

		foreach (var returnType in collectionAnalysis.GetCollectionPayloadTypes(methodSymbol.ReturnType))
		{
			if (collectionAnalysis.IsReadOnlyCollectionType(returnType) ||
				CollectionTypeAnalysis.IsBinaryBuffer(returnType) ||
				collectionAnalysis.IsServiceCollectionContract(returnType))
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(
				Rule,
				methodSymbol.Locations[0],
				methodSymbol.Name,
				returnType.ToDisplayString()));
			return;
		}
	}
}

/// <summary>
/// Extension methods for the <see cref="ITypeSymbol" /> interface.
/// </summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "False positive.")]
public static class ITypeSymbolExtensions
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	extension(ITypeSymbol? type)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	{
		/// <summary>
		/// Checks if the type implements <see cref="IEnumerable{T}" /> or is a collection type.
		/// </summary>
		/// <returns></returns>
		public bool IsCollectionType()
		{
			if (type is null)
				return false;

			// Check if the type implements IEnumerable<T> but is not string
			if (type.SpecialType == SpecialType.System_String)
				return false;

			foreach (var interfaceType in type.AllInterfaces)
			{
				if (interfaceType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
					return true;
			}

			return false;
		}
	}
}
