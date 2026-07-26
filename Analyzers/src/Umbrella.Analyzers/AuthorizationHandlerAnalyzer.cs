using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Umbrella.Analyzers;

/// <summary>
/// Roslyn analyzer that detects forbidden <c>AuthorizationHandlerContext.Fail()</c> calls inside
/// ASP.NET Core authorization handlers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AuthorizationHandlerAnalyzer : DiagnosticAnalyzer
{
	private const string AuthorizationHandlerContextMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext";
	private const string AuthorizationHandlerInterfaceMetadataName = "Microsoft.AspNetCore.Authorization.IAuthorizationHandler";
	private const string AuthorizationHandlerMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizationHandler`1";
	private const string ResourceAuthorizationHandlerMetadataName = "Microsoft.AspNetCore.Authorization.AuthorizationHandler`2";
	private const string FailMethodName = "Fail";

	/// <summary>
	/// Diagnostic emitted when an ASP.NET Core authorization handler calls
	/// <c>AuthorizationHandlerContext.Fail()</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor DoNotCallContextFailRule = new(
		id: "UA018",
		title: "Authorization handlers must not call context.Fail()",
		messageFormat: "Authorization handlers must approve successful cases with context.Succeed(requirement) and otherwise leave the requirement unsatisfied; do not call context.Fail()",
		category: "UmbrellaSecurity",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Calling context.Fail() creates an explicit authorization veto that cannot be reversed by another handler. Approve successful cases with context.Succeed(requirement), and leave all other cases unsatisfied so another handler can still approve them.");

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
			INamedTypeSymbol? authorizationHandlerInterfaceSymbol = startContext.Compilation.GetTypeByMetadataName(AuthorizationHandlerInterfaceMetadataName);
			INamedTypeSymbol? authorizationHandlerSymbol = startContext.Compilation.GetTypeByMetadataName(AuthorizationHandlerMetadataName);
			INamedTypeSymbol? resourceAuthorizationHandlerSymbol = startContext.Compilation.GetTypeByMetadataName(ResourceAuthorizationHandlerMetadataName);

			if (authHandlerContextSymbol is null ||
				(authorizationHandlerInterfaceSymbol is null &&
					authorizationHandlerSymbol is null &&
					resourceAuthorizationHandlerSymbol is null))
			{
				return;
			}

			startContext.RegisterOperationAction(
				ctx => AnalyzeInvocation(
					ctx,
					authHandlerContextSymbol,
					authorizationHandlerInterfaceSymbol,
					authorizationHandlerSymbol,
					resourceAuthorizationHandlerSymbol),
				OperationKind.Invocation);
		});
	}

	private static void AnalyzeInvocation(
		OperationAnalysisContext context,
		INamedTypeSymbol authHandlerContextSymbol,
		INamedTypeSymbol? authorizationHandlerInterfaceSymbol,
		INamedTypeSymbol? authorizationHandlerSymbol,
		INamedTypeSymbol? resourceAuthorizationHandlerSymbol)
	{
		var invocation = (IInvocationOperation)context.Operation;
		IMethodSymbol targetMethod = invocation.TargetMethod;

		if (targetMethod.Name != FailMethodName ||
			!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, authHandlerContextSymbol))
		{
			return;
		}

		INamedTypeSymbol? containingType = context.ContainingSymbol.ContainingType;
		if (containingType is null ||
			!IsAuthorizationHandler(
				containingType,
				authorizationHandlerInterfaceSymbol,
				authorizationHandlerSymbol,
				resourceAuthorizationHandlerSymbol))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(DoNotCallContextFailRule, invocation.Syntax.GetLocation()));
	}

	private static bool IsAuthorizationHandler(
		INamedTypeSymbol type,
		INamedTypeSymbol? authorizationHandlerInterfaceSymbol,
		INamedTypeSymbol? authorizationHandlerSymbol,
		INamedTypeSymbol? resourceAuthorizationHandlerSymbol)
	{
		if (authorizationHandlerInterfaceSymbol is not null &&
			type.AllInterfaces.Any(interfaceType =>
				SymbolEqualityComparer.Default.Equals(
					interfaceType.OriginalDefinition,
					authorizationHandlerInterfaceSymbol)))
		{
			return true;
		}

		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			INamedTypeSymbol originalDefinition = current.OriginalDefinition;

			if ((authorizationHandlerSymbol is not null &&
					SymbolEqualityComparer.Default.Equals(originalDefinition, authorizationHandlerSymbol)) ||
				(resourceAuthorizationHandlerSymbol is not null &&
					SymbolEqualityComparer.Default.Equals(originalDefinition, resourceAuthorizationHandlerSymbol)))
			{
				return true;
			}
		}

		return false;
	}
}
