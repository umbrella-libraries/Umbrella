using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Roslyn analyzer that detects forbidden <c>context.Fail()</c> calls inside <c>HandleRequirementAsync</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AuthorizationHandlerAnalyzer : DiagnosticAnalyzer
{
	private const string AuthorizationHandlerContextMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext";
	private const string HandleRequirementAsyncMethodName = "HandleRequirementAsync";
	private const string FailMethodName = "Fail";

	/// <summary>
	/// Diagnostic emitted when <c>context.Fail()</c> is called inside <c>HandleRequirementAsync</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor DoNotCallContextFailRule = new(
		id: "UA024",
		title: "Do not call context.Fail() in HandleRequirementAsync",
		messageFormat: "context.Fail() must not be called in HandleRequirementAsync; silently failing breaks the authorization pipeline — remove the call or throw an exception instead",
		category: "UmbrellaSecurity",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Calling context.Fail() inside HandleRequirementAsync silently denies authorization without propagating failure information. Remove the call; the framework will treat an un-succeeded requirement as a failure automatically.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DoNotCallContextFailRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			INamedTypeSymbol? authHandlerContextSymbol = startContext.Compilation.GetTypeByMetadataName(AuthorizationHandlerContextMetadataName);
			if (authHandlerContextSymbol is null)
				return;

			startContext.RegisterSyntaxNodeAction(
				ctx => AnalyzeInvocation(ctx, authHandlerContextSymbol),
				Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
		});
	}

	private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol authHandlerContextSymbol)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return;

		if (memberAccess.Name.Identifier.Text != FailMethodName)
			return;

		var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
		if (containingMethod?.Identifier.Text != HandleRequirementAsyncMethodName)
			return;

		var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
		if (receiverType is null || !SymbolEqualityComparer.Default.Equals(receiverType, authHandlerContextSymbol))
			return;

		if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			methodSymbol.Name != FailMethodName ||
			!SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, authHandlerContextSymbol))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(DoNotCallContextFailRule, invocation.GetLocation()));
	}
}
