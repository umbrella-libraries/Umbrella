using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Umbrella.Analyzers;

/// <summary>
/// Warns when an override of a standard Umbrella CRUD endpoint bypasses its base implementation. The base endpoint
/// coordinates the repository/data-service pipeline and its lifecycle hooks. Apply <c>[NonAction]</c> when an endpoint
/// is intentionally disabled.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerEndpointOverrideAnalyzer : DiagnosticAnalyzer
{
	private const string GenericRepositoryApiControllerMetadataName =
		"Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaGenericRepositoryApiController`11";

	private const string GenericRepositoryDataServiceApiControllerMetadataName =
		"Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaGenericRepositoryDataServiceApiController`9";

	private const string HttpMethodAttributeMetadataName =
		"Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute";

	private const string NonActionAttributeMetadataName =
		"Microsoft.AspNetCore.Mvc.NonActionAttribute";

	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA019";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Controller endpoint override must call base method",
		"Override of '{0}' in '{1}' does not call the overridden Umbrella endpoint on every normal return path. This skips the base endpoint pipeline. Use lifecycle hook overrides for custom logic, or apply [NonAction] to intentionally disable the endpoint.",
		"Architecture",
		DiagnosticSeverity.Warning,
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
		context.RegisterCompilationStartAction(
			compilationContext =>
			{
				INamedTypeSymbol? repositoryControllerType = compilationContext.Compilation.GetTypeByMetadataName(
					GenericRepositoryApiControllerMetadataName);

				INamedTypeSymbol? dataServiceControllerType = compilationContext.Compilation.GetTypeByMetadataName(
					GenericRepositoryDataServiceApiControllerMetadataName);

				INamedTypeSymbol? httpMethodAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
					HttpMethodAttributeMetadataName);

				if ((repositoryControllerType is null && dataServiceControllerType is null) ||
					httpMethodAttributeType is null)
				{
					return;
				}

				INamedTypeSymbol? nonActionAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
					NonActionAttributeMetadataName);

				var symbols = new EndpointSymbols(
					repositoryControllerType,
					dataServiceControllerType,
					httpMethodAttributeType,
					nonActionAttributeType);

				compilationContext.RegisterSymbolAction(
					symbolContext => AnalyzeBodylessOverride(symbolContext, symbols),
					SymbolKind.Method);

				compilationContext.RegisterOperationBlockAction(
					operationContext => AnalyzeOperationBlock(operationContext, symbols));
			});
	}

	private static void AnalyzeBodylessOverride(
		SymbolAnalysisContext context,
		EndpointSymbols symbols)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?
			.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax
			{
				Body: null,
				ExpressionBody: null
			})
		{
			return;
		}

		if (TryGetUmbrellaEndpoint(methodSymbol, symbols) is null ||
			HasNonActionAttribute(methodSymbol, symbols.NonActionAttribute))
		{
			return;
		}

		ReportDiagnostic(context.ReportDiagnostic, methodSymbol);
	}

	private static void AnalyzeOperationBlock(
		OperationBlockAnalysisContext context,
		EndpointSymbols symbols)
	{
		if (context.OwningSymbol is not IMethodSymbol methodSymbol ||
			TryGetUmbrellaEndpoint(methodSymbol, symbols) is null ||
			HasNonActionAttribute(methodSymbol, symbols.NonActionAttribute))
		{
			return;
		}

		IMethodSymbol? overriddenMethod = methodSymbol.OverriddenMethod;

		if (overriddenMethod is null ||
			!CallsOverriddenEndpointOnEveryNormalReturnPath(
				context.OperationBlocks,
				overriddenMethod,
				context.CancellationToken))
		{
			ReportDiagnostic(context.ReportDiagnostic, methodSymbol);
		}
	}

	private static IMethodSymbol? TryGetUmbrellaEndpoint(
		IMethodSymbol methodSymbol,
		EndpointSymbols symbols)
	{
		if (!methodSymbol.IsOverride ||
			methodSymbol.IsImplicitlyDeclared ||
			methodSymbol.DeclaringSyntaxReferences.Length == 0 ||
			!methodSymbol.Locations.Any(static location => location.IsInSource))
		{
			return null;
		}

		for (IMethodSymbol? current = methodSymbol.OverriddenMethod;
			current is not null;
			current = current.OverriddenMethod)
		{
			if (!IsDeclaredOnEndpointController(current.ContainingType, symbols))
				continue;

			if (current.DeclaredAccessibility == Accessibility.Public &&
				current.GetAttributes().Any(
					attribute => IsOrInheritsFrom(
						attribute.AttributeClass,
						symbols.HttpMethodAttribute)))
			{
				return current;
			}
		}

		return null;
	}

	private static bool IsDeclaredOnEndpointController(
		INamedTypeSymbol containingType,
		EndpointSymbols symbols)
	{
		return SymbolEqualityComparer.Default.Equals(
				containingType.OriginalDefinition,
				symbols.RepositoryController) ||
			SymbolEqualityComparer.Default.Equals(
				containingType.OriginalDefinition,
				symbols.DataServiceController);
	}

	private static bool HasNonActionAttribute(
		IMethodSymbol methodSymbol,
		INamedTypeSymbol? nonActionAttribute)
	{
		if (nonActionAttribute is null)
			return false;

		for (IMethodSymbol? current = methodSymbol; current is not null; current = current.OverriddenMethod)
		{
			if (current.GetAttributes().Any(
				attribute => IsOrInheritsFrom(attribute.AttributeClass, nonActionAttribute)))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsOrInheritsFrom(
		INamedTypeSymbol? type,
		INamedTypeSymbol candidateBaseType)
	{
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(
				current.OriginalDefinition,
				candidateBaseType.OriginalDefinition))
			{
				return true;
			}
		}

		return false;
	}

	private static bool CallsOverriddenEndpointOnEveryNormalReturnPath(
		ImmutableArray<IOperation> operationBlocks,
		IMethodSymbol overriddenMethod,
		CancellationToken cancellationToken)
	{
		ControlFlowGraph? graph = CreateControlFlowGraph(operationBlocks, cancellationToken);

		if (graph is null)
			return false;

		var blocksWithBaseCall = new HashSet<BasicBlock>();

		foreach (BasicBlock block in graph.Blocks)
		{
			if (block.Operations.Any(operation => ContainsOverriddenBaseCall(operation, overriddenMethod)) ||
				(block.BranchValue is not null &&
					ContainsOverriddenBaseCall(block.BranchValue, overriddenMethod)))
			{
				_ = blocksWithBaseCall.Add(block);
			}
		}

		return blocksWithBaseCall.Count > 0 &&
			!CanReachExitWithoutBaseCall(graph, blocksWithBaseCall);
	}

	private static ControlFlowGraph? CreateControlFlowGraph(
		ImmutableArray<IOperation> operationBlocks,
		CancellationToken cancellationToken)
	{
		foreach (IOperation operationBlock in operationBlocks)
		{
			if (operationBlock is IMethodBodyOperation methodBody)
				return ControlFlowGraph.Create(methodBody, cancellationToken);

			if (operationBlock.Parent is IMethodBodyOperation parentMethodBody)
				return ControlFlowGraph.Create(parentMethodBody, cancellationToken);

			if (operationBlock is IBlockOperation block)
				return ControlFlowGraph.Create(block, cancellationToken);
		}

		return null;
	}

	private static bool ContainsOverriddenBaseCall(
		IOperation operation,
		IMethodSymbol overriddenMethod)
	{
		if (operation is IAnonymousFunctionOperation or ILocalFunctionOperation)
			return false;

		if (operation is IInvocationOperation invocation &&
			invocation.Syntax is InvocationExpressionSyntax
			{
				Expression: MemberAccessExpressionSyntax
				{
					Expression: BaseExpressionSyntax
				}
			} &&
			SymbolEqualityComparer.Default.Equals(
				invocation.TargetMethod.OriginalDefinition,
				overriddenMethod.OriginalDefinition))
		{
			return true;
		}

		return operation.ChildOperations.Any(
			child => ContainsOverriddenBaseCall(child, overriddenMethod));
	}

	private static bool CanReachExitWithoutBaseCall(
		ControlFlowGraph graph,
		HashSet<BasicBlock> blocksWithBaseCall)
	{
		BasicBlock entry = graph.Blocks[0];
		var visited = new HashSet<BasicBlock>();
		var pending = new Queue<BasicBlock>();
		pending.Enqueue(entry);

		while (pending.Count > 0)
		{
			BasicBlock block = pending.Dequeue();

			if (!visited.Add(block))
				continue;

			if (block.Kind == BasicBlockKind.Exit)
				return true;

			if (blocksWithBaseCall.Contains(block))
				continue;

			EnqueueDestination(block.FallThroughSuccessor, pending);
			EnqueueDestination(block.ConditionalSuccessor, pending);
		}

		return false;
	}

	private static void EnqueueDestination(
		ControlFlowBranch? branch,
		Queue<BasicBlock> pending)
	{
		if (branch?.Destination is { } destination)
			pending.Enqueue(destination);
	}

	private static void ReportDiagnostic(
		Action<Diagnostic> reportDiagnostic,
		IMethodSymbol methodSymbol)
	{
		Location? location = methodSymbol.Locations.FirstOrDefault(static x => x.IsInSource);

		if (location is null)
			return;

		reportDiagnostic(
			Diagnostic.Create(
				Rule,
				location,
				methodSymbol.Name,
				methodSymbol.ContainingType.Name));
	}

	private sealed record EndpointSymbols(
		INamedTypeSymbol? RepositoryController,
		INamedTypeSymbol? DataServiceController,
		INamedTypeSymbol HttpMethodAttribute,
		INamedTypeSymbol? NonActionAttribute);
}
