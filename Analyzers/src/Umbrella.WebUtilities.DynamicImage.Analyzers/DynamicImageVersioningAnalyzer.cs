using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.WebUtilities.DynamicImage.Analyzers;

/// <summary>
/// Roslyn analyzer that enforces the DynamicImage URL/version-token pairing convention for Umbrella model types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DynamicImageVersioningAnalyzer : DiagnosticAnalyzer
{
	private static readonly string[] _dynamicImagePropertyIndicators =
	[
		"image",
		"thumbnail",
		"photo",
		"picture",
		"logo",
		"icon",
		"avatar",
		"banner"
	];

	/// <summary>
	/// Diagnostic rule that requires DynamicImage URL properties to declare matching version token properties.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingVersionTokenPropertyRule = new(
		id: "UA015",
		title: "DynamicImage URL properties must declare matching version token properties",
		messageFormat: "Property '{0}' in model type '{1}' must declare a matching '{2}' property when DynamicImage URL fingerprinting is explicitly enabled",
		category: "DynamicImageVersioning",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "DynamicImage URL properties in Umbrella model types must declare matching VersionToken properties when middleware URL fingerprinting is explicitly enabled.");

	/// <summary>
	/// Diagnostic rule that requires DynamicImage URL assignments to assign the matching version token property.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingVersionTokenAssignmentRule = new(
		id: "UA016",
		title: "DynamicImage URL assignments must also assign matching version tokens",
		messageFormat: "Assignment to '{0}' must also assign '{1}' in the same model construction or update flow when DynamicImage URL fingerprinting is explicitly enabled",
		category: "DynamicImageVersioning",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "DynamicImage URL assignments in Umbrella model construction and mapping flows must also assign the matching VersionToken property when middleware URL fingerprinting is explicitly enabled.");

	/// <summary>
	/// Diagnostic rule that requires UmbrellaDynamicImage and DynamicImage tag helper usages to assign the VersionToken input.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingVersionTokenUsageRule = new(
		id: "UA017",
		title: "DynamicImage UI usages must assign VersionToken",
		messageFormat: "DynamicImage usage bound to '{0}' must also assign '{1}' when URL fingerprinting is explicitly enabled",
		category: "DynamicImageVersioning",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "UmbrellaDynamicImage and DynamicImage tag helper usages bound to DynamicImage URL model properties must also assign the matching VersionToken input when middleware URL fingerprinting is explicitly enabled.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingVersionTokenPropertyRule, MissingVersionTokenAssignmentRule, MissingVersionTokenUsageRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			var state = new DynamicImageVersioningState();

			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeDynamicImageRegistrationInvocation(syntaxContext, state), SyntaxKind.InvocationExpression);
			startContext.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, state), SymbolKind.NamedType);
			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeObjectInitializer(syntaxContext, state), SyntaxKind.ObjectInitializerExpression);
			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeAssignmentExpression(syntaxContext, state), SyntaxKind.SimpleAssignmentExpression);
			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeBlock(syntaxContext, state), SyntaxKind.Block);
			startContext.RegisterCompilationEndAction(compilationContext => ReportDiagnostics(compilationContext, state));
		});
	}

	private static void AnalyzeDynamicImageRegistrationInvocation(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!IsDynamicImageRegistrationMethod(methodSymbol))
		{
			return;
		}

		ArgumentSyntax? optionsBuilderArgument = GetOptionsBuilderArgument(invocation, methodSymbol);

		if (optionsBuilderArgument is not null && HasExplicitTrueFingerprintingAssignment(optionsBuilderArgument.Expression, context.SemanticModel))
			state.MarkExplicitlyEnabled();
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context, DynamicImageVersioningState state)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		if (!IsRelevantModelType(typeSymbol))
			return;

		var properties = typeSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(x => !x.IsStatic && x.Parameters.Length is 0)
			.ToImmutableArray();

		foreach (IPropertySymbol property in properties)
		{
			if (!TryGetDynamicImageVersionTokenPropertyName(property, out string versionTokenPropertyName))
				continue;

			if (TryGetMatchingProperty(properties, versionTokenPropertyName) is { Type.SpecialType: SpecialType.System_String })
				continue;

			Location location = property.Locations.FirstOrDefault() ?? Location.None;
			state.AddDiagnostic(Diagnostic.Create(
				MissingVersionTokenPropertyRule,
				location,
				property.Name,
				typeSymbol.Name,
				versionTokenPropertyName));
		}
	}

	private static void AnalyzeObjectInitializer(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var initializer = (InitializerExpressionSyntax)context.Node;

		if (GetCreatedTypeSymbol(context, initializer) is not { } createdType || !IsRelevantModelType(createdType))
			return;

		AssignmentExpressionSyntax[] assignments = [.. initializer.Expressions.OfType<AssignmentExpressionSyntax>()];

		foreach (AssignmentExpressionSyntax assignment in assignments)
		{
			if (!TryGetDynamicImageVersionTokenPropertyName(context.SemanticModel, createdType, assignment.Left, context.CancellationToken, out string urlPropertyName, out string versionTokenPropertyName))
				continue;

			if (TryGetMatchingProperty(createdType.GetMembers().OfType<IPropertySymbol>().ToImmutableArray(), versionTokenPropertyName) is not { Type.SpecialType: SpecialType.System_String })
				continue;

			bool hasMatchingAssignment = assignments.Any(x =>
				!ReferenceEquals(x, assignment) &&
				TryGetAssignedPropertyName(context.SemanticModel, createdType, x.Left, context.CancellationToken, out string propertyName) &&
				string.Equals(propertyName, versionTokenPropertyName, StringComparison.Ordinal));

			if (hasMatchingAssignment)
				continue;

			state.AddDiagnostic(Diagnostic.Create(
				MissingVersionTokenAssignmentRule,
				GetAssignmentLocation(assignment.Left),
				urlPropertyName,
				versionTokenPropertyName));
		}
	}

	private static void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var assignment = (AssignmentExpressionSyntax)context.Node;

		if (assignment.Parent is InitializerExpressionSyntax || assignment.Parent is not ExpressionStatementSyntax)
			return;

		if (!TryGetAssignedModelProperty(context.SemanticModel, assignment.Left, context.CancellationToken, out IPropertySymbol? propertySymbol, out ISymbol? receiverSymbol, out string receiverText))
			return;

		if (propertySymbol is null)
			return;

		if (!TryGetDynamicImageVersionTokenPropertyName(propertySymbol, out string versionTokenPropertyName))
			return;

		var properties = propertySymbol.ContainingType.GetMembers().OfType<IPropertySymbol>().ToImmutableArray();
		if (TryGetMatchingProperty(properties, versionTokenPropertyName) is not { Type.SpecialType: SpecialType.System_String })
			return;

		BlockSyntax? containingBlock = assignment.FirstAncestorOrSelf<BlockSyntax>();

		if (containingBlock is null)
			return;

		bool hasMatchingAssignment = containingBlock.Statements
			.OfType<ExpressionStatementSyntax>()
			.Select(x => x.Expression)
			.OfType<AssignmentExpressionSyntax>()
			.Where(x => !ReferenceEquals(x, assignment))
			.Any(x => IsMatchingVersionTokenAssignment(context.SemanticModel, x, versionTokenPropertyName, receiverSymbol, receiverText, context.CancellationToken));

		if (hasMatchingAssignment)
			return;

		state.AddDiagnostic(Diagnostic.Create(
			MissingVersionTokenAssignmentRule,
			GetAssignmentLocation(assignment.Left),
			propertySymbol.Name,
			versionTokenPropertyName));
	}

	private static void AnalyzeBlock(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var block = (BlockSyntax)context.Node;

		AnalyzeBlazorComponentUsages(context, state, block);
		AnalyzeTagHelperUsages(context, state, block);
	}

	private static void ReportDiagnostics(CompilationAnalysisContext context, DynamicImageVersioningState state)
	{
		if (!state.IsExplicitlyEnabled)
			return;

		foreach (Diagnostic diagnostic in state.Diagnostics)
			context.ReportDiagnostic(diagnostic);
	}

	private static void AnalyzeBlazorComponentUsages(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state, BlockSyntax block)
	{
		var statements = block.Statements;

		for (int i = 0; i < statements.Count; i++)
		{
			if (!TryGetInvocationStatement(statements[i], out InvocationExpressionSyntax? invocation) || invocation is null)
				continue;

			if (!TryGetInvocationReceiverText(invocation, out string? receiverText) ||
				!IsOpenComponentInvocation(context.SemanticModel, invocation, context.CancellationToken, out bool isDynamicImageComponent) ||
				!isDynamicImageComponent)
			{
				continue;
			}

			int nestingDepth = 1;
			string? urlPropertyName = null;
			string? versionTokenPropertyName = null;
			Location? diagnosticLocation = null;
			bool hasVersionTokenAssignment = false;

			for (int j = i + 1; j < statements.Count; j++)
			{
				if (!TryGetInvocationStatement(statements[j], out InvocationExpressionSyntax? nextInvocation) || nextInvocation is null)
					continue;

				if (!TryGetInvocationReceiverText(nextInvocation, out string? nextReceiverText) ||
					!string.Equals(receiverText, nextReceiverText, StringComparison.Ordinal))
				{
					continue;
				}

				if (IsOpenComponentInvocation(context.SemanticModel, nextInvocation, context.CancellationToken, out _))
				{
					nestingDepth++;
					continue;
				}

				if (IsCloseComponentInvocation(context.SemanticModel, nextInvocation))
				{
					nestingDepth--;

					if (nestingDepth is 0)
					{
						if (urlPropertyName is not null && versionTokenPropertyName is not null && !hasVersionTokenAssignment)
						{
							state.AddDiagnostic(Diagnostic.Create(
								MissingVersionTokenUsageRule,
								diagnosticLocation ?? nextInvocation.GetLocation(),
								urlPropertyName,
								versionTokenPropertyName));
						}

						i = j;
						break;
					}

					continue;
				}

				if (nestingDepth is not 1 ||
					!TryGetRenderTreeAttribute(nextInvocation, context.SemanticModel, context.CancellationToken, out string attributeName, out ExpressionSyntax? valueExpression))
				{
					continue;
				}

				if (string.Equals(attributeName, "Url", StringComparison.Ordinal) &&
					valueExpression is not null &&
					TryGetDynamicImageModelPropertyReference(context.SemanticModel, valueExpression, context.CancellationToken, out IPropertySymbol? propertySymbol, out string expectedVersionTokenPropertyName))
				{
					urlPropertyName = propertySymbol.Name;
					versionTokenPropertyName = expectedVersionTokenPropertyName;
					diagnosticLocation = GetInvocationLocation(nextInvocation);
					continue;
				}

				if (string.Equals(attributeName, "VersionToken", StringComparison.Ordinal))
					hasVersionTokenAssignment = true;
			}
		}
	}

	private static void AnalyzeTagHelperUsages(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state, BlockSyntax block)
	{
		var urlAssignments = new Dictionary<string, (string UrlPropertyName, string VersionTokenPropertyName, Location Location)>(StringComparer.Ordinal);
		var versionTokenAssignments = new HashSet<string>(StringComparer.Ordinal);

		foreach (StatementSyntax statement in block.Statements)
		{
			if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
				continue;

			if (!TryGetAssignedTagHelperProperty(context.SemanticModel, assignment.Left, context.CancellationToken, out string receiverText, out string propertyName))
				continue;

			if (string.Equals(propertyName, "VersionToken", StringComparison.Ordinal))
			{
				versionTokenAssignments.Add(receiverText);
				continue;
			}

			if (!string.Equals(propertyName, "Src", StringComparison.Ordinal) ||
				!TryGetDynamicImageModelPropertyReference(context.SemanticModel, assignment.Right, context.CancellationToken, out IPropertySymbol? propertySymbol, out string versionTokenPropertyName))
			{
				continue;
			}

			urlAssignments[receiverText] = (propertySymbol.Name, versionTokenPropertyName, GetAssignmentLocation(assignment.Left));
		}

		foreach (KeyValuePair<string, (string UrlPropertyName, string VersionTokenPropertyName, Location Location)> entry in urlAssignments)
		{
			string receiverText = entry.Key;

			if (versionTokenAssignments.Contains(receiverText))
				continue;

			state.AddDiagnostic(Diagnostic.Create(
				MissingVersionTokenUsageRule,
				entry.Value.Location,
				entry.Value.UrlPropertyName,
				entry.Value.VersionTokenPropertyName));
		}
	}

	private static bool IsMatchingVersionTokenAssignment(
		SemanticModel semanticModel,
		AssignmentExpressionSyntax assignment,
		string versionTokenPropertyName,
		ISymbol? expectedReceiverSymbol,
		string expectedReceiverText,
		CancellationToken cancellationToken)
	{
		if (!TryGetAssignedModelProperty(semanticModel, assignment.Left, cancellationToken, out IPropertySymbol? propertySymbol, out ISymbol? receiverSymbol, out string receiverText))
			return false;

		if (!string.Equals(propertySymbol.Name, versionTokenPropertyName, StringComparison.Ordinal))
			return false;

		if (expectedReceiverSymbol is not null && receiverSymbol is not null)
			return SymbolEqualityComparer.Default.Equals(expectedReceiverSymbol, receiverSymbol);

		return string.Equals(receiverText, expectedReceiverText, StringComparison.Ordinal);
	}

	private static bool TryGetAssignedModelProperty(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		[NotNullWhen(true)] out IPropertySymbol? propertySymbol,
		out ISymbol? receiverSymbol,
		out string receiverText)
	{
		propertySymbol = null;
		receiverSymbol = null;
		receiverText = string.Empty;

		if (expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		propertySymbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol as IPropertySymbol;

		if (propertySymbol is null || !IsRelevantModelType(propertySymbol.ContainingType))
			return false;

		if (propertySymbol.IsStatic || propertySymbol.Parameters.Length > 0)
			return false;

		receiverText = memberAccess.Expression.ToString();
		receiverSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;

		return true;
	}

	private static bool TryGetAssignedTagHelperProperty(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string receiverText,
		out string propertyName)
	{
		receiverText = string.Empty;
		propertyName = string.Empty;

		if (expression is not MemberAccessExpressionSyntax memberAccess ||
			semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol is not IPropertySymbol propertySymbol ||
			!IsDynamicImageTagHelperProperty(propertySymbol))
		{
			return false;
		}

		receiverText = memberAccess.Expression.ToString();
		propertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetAssignedPropertyName(
		SemanticModel semanticModel,
		INamedTypeSymbol createdType,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string propertyName)
	{
		propertyName = string.Empty;

		if (semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is not IPropertySymbol propertySymbol)
			return false;

		if (!SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, createdType))
			return false;

		propertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetDynamicImageVersionTokenPropertyName(
		SemanticModel semanticModel,
		INamedTypeSymbol createdType,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string urlPropertyName,
		out string versionTokenPropertyName)
	{
		urlPropertyName = string.Empty;
		versionTokenPropertyName = string.Empty;

		if (!TryGetAssignedPropertyName(semanticModel, createdType, expression, cancellationToken, out string propertyName))
			return false;

		IPropertySymbol? propertySymbol = createdType.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();

		if (propertySymbol is null)
			return false;

		if (!TryGetDynamicImageVersionTokenPropertyName(propertySymbol, out versionTokenPropertyName))
			return false;

		urlPropertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetDynamicImageVersionTokenPropertyName(IPropertySymbol propertySymbol, out string versionTokenPropertyName)
	{
		versionTokenPropertyName = string.Empty;

		if (propertySymbol.Type.SpecialType is not SpecialType.System_String)
			return false;

		if (!TryGetDynamicImageUrlPrefix(propertySymbol.Name, out string prefix))
			return false;

		versionTokenPropertyName = $"{prefix}VersionToken";
		return true;
	}

	private static bool TryGetDynamicImageModelPropertyReference(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		[NotNullWhen(true)] out IPropertySymbol? propertySymbol,
		out string versionTokenPropertyName)
	{
		propertySymbol = null;
		versionTokenPropertyName = string.Empty;

		ExpressionSyntax unwrappedExpression = UnwrapExpression(expression, semanticModel, cancellationToken);

		propertySymbol = semanticModel.GetSymbolInfo(unwrappedExpression, cancellationToken).Symbol as IPropertySymbol;

		return propertySymbol is not null &&
			   IsRelevantModelType(propertySymbol.ContainingType) &&
			   TryGetDynamicImageVersionTokenPropertyName(propertySymbol, out versionTokenPropertyName);
	}

	private static bool TryGetDynamicImageUrlPrefix(string propertyName, out string prefix)
	{
		prefix = string.Empty;

		if (!propertyName.EndsWith("Url", StringComparison.Ordinal) || propertyName.Length <= 3)
			return false;

		string candidatePrefix = propertyName[..^3];

		if (!_dynamicImagePropertyIndicators.Any(x => candidatePrefix.Contains(x, StringComparison.OrdinalIgnoreCase)))
			return false;

		prefix = candidatePrefix;
		return true;
	}

	private static IPropertySymbol? TryGetMatchingProperty(ImmutableArray<IPropertySymbol> properties, string propertyName)
		=> properties.FirstOrDefault(x => string.Equals(x.Name, propertyName, StringComparison.Ordinal));

	private static bool TryGetInvocationStatement(StatementSyntax statement, [NotNullWhen(true)] out InvocationExpressionSyntax? invocation)
	{
		if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax expression })
		{
			invocation = expression;
			return true;
		}

		invocation = null;
		return false;
	}

	private static bool TryGetInvocationReceiverText(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out string? receiverText)
	{
		receiverText = null;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		receiverText = memberAccess.Expression.ToString();
		return true;
	}

	private static bool IsOpenComponentInvocation(
		SemanticModel semanticModel,
		InvocationExpressionSyntax invocation,
		CancellationToken cancellationToken,
		out bool isDynamicImageComponent)
	{
		isDynamicImageComponent = false;

		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!string.Equals(methodSymbol.Name, "OpenComponent", StringComparison.Ordinal))
		{
			return false;
		}

		isDynamicImageComponent = methodSymbol.TypeArguments.Length is 1 &&
			string.Equals(methodSymbol.TypeArguments[0].ToDisplayString(), "Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage", StringComparison.Ordinal);

		return true;
	}

	private static bool IsCloseComponentInvocation(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
		=> semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "CloseComponent" };

	private static bool TryGetRenderTreeAttribute(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string attributeName,
		out ExpressionSyntax? valueExpression)
	{
		attributeName = string.Empty;
		valueExpression = null;

		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { Name: "AddAttribute" } ||
			invocation.ArgumentList.Arguments.Count < 2)
		{
			return false;
		}

		Optional<object?> attributeNameValue = semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[1].Expression, cancellationToken);

		if (!attributeNameValue.HasValue || attributeNameValue.Value is not string attributeNameString)
			return false;

		attributeName = attributeNameString;
		valueExpression = invocation.ArgumentList.Arguments.Count > 2 ? invocation.ArgumentList.Arguments[2].Expression : null;

		return true;
	}

	private static Location GetInvocationLocation(InvocationExpressionSyntax invocation)
	{
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
			return memberAccess.Name.GetLocation();

		return invocation.GetLocation();
	}

	private static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		ExpressionSyntax currentExpression = expression;

		while (true)
		{
			switch (currentExpression)
			{
				case ParenthesizedExpressionSyntax parenthesizedExpression:
					currentExpression = parenthesizedExpression.Expression;
					continue;
				case CastExpressionSyntax castExpression:
					currentExpression = castExpression.Expression;
					continue;
				case InvocationExpressionSyntax invocation when IsRuntimeTypeCheckInvocation(invocation, semanticModel, cancellationToken):
					currentExpression = invocation.ArgumentList.Arguments[0].Expression;
					continue;
				default:
					return currentExpression;
			}
		}
	}

	private static bool IsRuntimeTypeCheckInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (invocation.ArgumentList.Arguments.Count is 0 ||
			semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
		{
			return false;
		}

		return string.Equals(methodSymbol.Name, "TypeCheck", StringComparison.Ordinal) &&
			   string.Equals(methodSymbol.ContainingType.ToDisplayString(), "Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers", StringComparison.Ordinal);
	}

	private static INamedTypeSymbol? GetCreatedTypeSymbol(SyntaxNodeAnalysisContext context, InitializerExpressionSyntax initializer)
	{
		return initializer.Parent switch
		{
			ObjectCreationExpressionSyntax objectCreation => context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type as INamedTypeSymbol,
			ImplicitObjectCreationExpressionSyntax objectCreation => context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type as INamedTypeSymbol,
			_ => null
		};
	}

	private static Location GetAssignmentLocation(ExpressionSyntax expression)
	{
		return expression switch
		{
			IdentifierNameSyntax identifierName => identifierName.GetLocation(),
			MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
			_ => expression.GetLocation()
		};
	}

	private static bool IsRelevantModelType(INamedTypeSymbol? typeSymbol)
	{
		if (typeSymbol is null)
			return false;

		string typeName = typeSymbol.Name;

		return typeName.EndsWith("Model", StringComparison.Ordinal) ||
			   typeName.EndsWith("ModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModel", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("QueryResult", StringComparison.Ordinal);
	}

	private static bool IsDynamicImageTagHelperProperty(IPropertySymbol propertySymbol)
	{
		if (propertySymbol.Name is not ("Src" or "VersionToken"))
			return false;

		for (INamedTypeSymbol? type = propertySymbol.ContainingType; type is not null; type = type.BaseType)
		{
			if (string.Equals(type.ToDisplayString(), "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelperBase", StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private sealed class DynamicImageVersioningState
	{
		private readonly ConcurrentBag<Diagnostic> _diagnostics = [];
		private int _isExplicitlyEnabled;

		public ImmutableArray<Diagnostic> Diagnostics => [.. _diagnostics];

		public bool IsExplicitlyEnabled => _isExplicitlyEnabled is 1;

		public void AddDiagnostic(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

		public void MarkExplicitlyEnabled() => _ = System.Threading.Interlocked.Exchange(ref _isExplicitlyEnabled, 1);
	}

	private static bool IsDynamicImageRegistrationMethod(IMethodSymbol methodSymbol)
	{
		IMethodSymbol comparisonMethod = methodSymbol.ReducedFrom ?? methodSymbol;

		if (!string.Equals(comparisonMethod.Name, "AddUmbrellaWebUtilitiesDynamicImage", StringComparison.Ordinal))
			return false;

		if (!string.Equals(comparisonMethod.ContainingNamespace.ToDisplayString(), "Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
			return false;

		IParameterSymbol? optionsBuilderParameter = comparisonMethod.Parameters.FirstOrDefault(x => string.Equals(x.Name, "optionsBuilder", StringComparison.Ordinal));

		if (optionsBuilderParameter?.Type is not INamedTypeSymbol delegateType)
			return false;

		if (!string.Equals(delegateType.OriginalDefinition.ToDisplayString(), "System.Action<T1, T2>", StringComparison.Ordinal))
			return false;

		return delegateType.TypeArguments.Length is 2 &&
			   string.Equals(delegateType.TypeArguments[1].ToDisplayString(), "Umbrella.WebUtilities.DynamicImage.Middleware.Options.DynamicImageMiddlewareOptions", StringComparison.Ordinal);
	}

	private static ArgumentSyntax? GetOptionsBuilderArgument(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol)
	{
		ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;

		for (int i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
		{
			ArgumentSyntax argument = invocation.ArgumentList.Arguments[i];
			IParameterSymbol? parameter = null;

			if (argument.NameColon is not null)
			{
				string parameterName = argument.NameColon.Name.Identifier.ValueText;
				parameter = parameters.FirstOrDefault(x => string.Equals(x.Name, parameterName, StringComparison.Ordinal));
			}
			else if (i < parameters.Length)
			{
				parameter = parameters[i];
			}

			if (parameter is not null && string.Equals(parameter.Name, "optionsBuilder", StringComparison.Ordinal))
				return argument;
		}

		return null;
	}

	private static bool HasExplicitTrueFingerprintingAssignment(ExpressionSyntax expression, SemanticModel semanticModel)
	{
		string? optionsParameterName = GetOptionsParameterName(expression);

		if (string.IsNullOrWhiteSpace(optionsParameterName))
			return false;

		IEnumerable<AssignmentExpressionSyntax> assignments = expression switch
		{
			ParenthesizedLambdaExpressionSyntax { Body: BlockSyntax body } => body.DescendantNodes().OfType<AssignmentExpressionSyntax>(),
			ParenthesizedLambdaExpressionSyntax { Body: AssignmentExpressionSyntax assignment } => [assignment],
			SimpleLambdaExpressionSyntax { Body: BlockSyntax body } => body.DescendantNodes().OfType<AssignmentExpressionSyntax>(),
			SimpleLambdaExpressionSyntax { Body: AssignmentExpressionSyntax assignment } => [assignment],
			AnonymousMethodExpressionSyntax { Block: { } body } => body.DescendantNodes().OfType<AssignmentExpressionSyntax>(),
			_ => []
		};

		foreach (AssignmentExpressionSyntax assignment in assignments)
		{
			if (assignment.Left is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver, Name.Identifier.ValueText: "EnableUrlFingerprinting" })
				continue;

			if (!string.Equals(receiver.Identifier.ValueText, optionsParameterName, StringComparison.Ordinal))
				continue;

			Optional<object?> constantValue = semanticModel.GetConstantValue(assignment.Right);
			if (constantValue.HasValue && constantValue.Value is true)
				return true;
		}

		return false;
	}

	private static string? GetOptionsParameterName(ExpressionSyntax expression)
	{
		return expression switch
		{
			ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.ParameterList.Parameters.Count > 0
				=> parenthesizedLambda.ParameterList.Parameters[^1].Identifier.ValueText,
			SimpleLambdaExpressionSyntax simpleLambda
				=> simpleLambda.Parameter.Identifier.ValueText,
			AnonymousMethodExpressionSyntax anonymousMethod when anonymousMethod.ParameterList?.Parameters.Count > 0
				=> anonymousMethod.ParameterList.Parameters[^1].Identifier.ValueText,
			_ => null
		};
	}
}
