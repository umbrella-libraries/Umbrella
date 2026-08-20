#if NET10_0_OR_GREATER
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Transforms OpenAPI operations so that query parameters bound by the Umbrella Data Expression model binders are
/// documented using the JSON format that API consumers actually have to send.
/// </summary>
/// <remarks>
/// <para>
/// Action parameters typed as <see cref="SortExpression{TItem}"/> or <see cref="FilterExpression{TItem}"/> are bound by
/// the Umbrella Data Expression model binders, which read a single query string value containing a JSON document and
/// deserialize it to <see cref="SortExpressionDescriptor"/> or <see cref="FilterExpressionDescriptor"/> before
/// converting it to the expression types. Without this transformer the generated document describes the server side
/// expression types instead, including the <see cref="System.Linq.Expressions.Expression"/> tree they contain, which is
/// meaningless to a consumer and impossible to send.
/// </para>
/// <para>
/// Each affected parameter is rewritten to use the OpenAPI parameter <c>content</c> field with an
/// <c>application/json</c> media type. That is the mechanism for describing a parameter whose value is a JSON document,
/// whereas the <c>schema</c> field describes form encoded values.
/// </para>
/// <para>
/// Enum properties on the descriptors are documented using their names because the model binders always deserialize
/// using a <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>, irrespective of how the application has
/// configured enum serialization elsewhere.
/// </para>
/// </remarks>
internal sealed class UmbrellaDataExpressionParameterOperationTransformer : IOpenApiOperationTransformer
{
	private static readonly ConcurrentDictionary<Type, string[]> _enumMemberNameCache = new();

	/// <inheritdoc />
	public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(operation);
		Guard.IsNotNull(context);

		if (operation.Parameters is not { Count: > 0 })
			return;

		List<ParameterDescriptor?> owners = MapParameterOwners(operation, context.Description);

		// Parameters are grouped by the action parameter they came from. A parameter whose type is not treated as a
		// simple type is flattened by ApiExplorer into one entry per property, so a single Data Expression parameter
		// arrives as several entries that all share one descriptor and must be replaced together.
		foreach (IGrouping<ParameterDescriptor, ApiParameterDescription> group in context.Description.ParameterDescriptions
			.Where(x => x.ParameterDescriptor is not null)
			.GroupBy(x => x.ParameterDescriptor))
		{
			Type parameterType = group.Key.ParameterType;
			parameterType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

			var (descriptorType, isCollection) = GetDescriptorTypeData(parameterType);

			if (descriptorType is null)
				continue;

			if (!TryReplaceParameters(operation, owners, group.Key, out OpenApiParameter? parameter))
				continue;

			OpenApiSchema descriptorSchema = await context.GetOrCreateSchemaAsync(descriptorType, null, cancellationToken).ConfigureAwait(false);

			ApplyEnumMemberNames(descriptorSchema, descriptorType);

			IOpenApiSchema schema = isCollection
				? new OpenApiSchema
				{
					Type = JsonSchemaType.Array,
					Items = descriptorSchema
				}
				: descriptorSchema;

			// The value is a JSON document held in a single query string value, so it is described using the parameter
			// content field. Specifying both content and schema is invalid, so the generated schema is discarded.
			parameter.Schema = null;
			parameter.Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
			{
				["application/json"] = new OpenApiMediaType
				{
					Schema = schema
				}
			};

			if (string.IsNullOrWhiteSpace(parameter.Description))
				parameter.Description = BuildDescription(descriptorType, isCollection);
		}
	}

	/// <summary>
	/// Determines whether the supplied model type is bound by one of the Data Expression model binders and, if so,
	/// resolves the descriptor type that consumers actually send.
	/// </summary>
	/// <param name="modelType">The model type of the action parameter.</param>
	/// <returns>
	/// The descriptor type together with a value specifying whether the parameter accepts a collection, or
	/// <see langword="null"/> when the parameter is not bound by a Data Expression model binder.
	/// </returns>
	/// <remarks>
	/// The checks performed here mirror those in the Data Expression model binder providers so that the generated
	/// document and the binding behaviour cannot drift apart.
	/// </remarks>
	private static (Type? DescriptorType, bool IsCollection) GetDescriptorTypeData(Type modelType)
	{
		Type? descriptorType = GetDescriptorType(modelType);

		if (descriptorType is not null)
			return (descriptorType, false);

		var (isEnumerable, elementType) = modelType.GetIEnumerableTypeData();

		if (isEnumerable && elementType is not null)
		{
			descriptorType = GetDescriptorType(elementType);

			if (descriptorType is not null)
				return (descriptorType, true);
		}

		return (null, false);
	}

	/// <summary>
	/// Resolves the descriptor type for a single, non enumerable type.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The descriptor type, or <see langword="null"/> when the type is not a Data Expression type.</returns>
	private static Type? GetDescriptorType(Type type)
	{
		if (type == typeof(SortExpressionDescriptor) || type == typeof(FilterExpressionDescriptor))
			return type;

		if (!type.IsGenericType)
			return null;

		Type genericTypeDefinition = type.GetGenericTypeDefinition();

		if (genericTypeDefinition == typeof(SortExpression<>))
			return typeof(SortExpressionDescriptor);

		return genericTypeDefinition == typeof(FilterExpression<>) ? typeof(FilterExpressionDescriptor) : null;
	}

	/// <summary>
	/// Works out which action parameter each generated OpenAPI parameter came from.
	/// </summary>
	/// <param name="operation">The operation.</param>
	/// <param name="description">The API description.</param>
	/// <returns>The owning action parameter of each entry in <see cref="OpenApiOperation.Parameters"/>.</returns>
	/// <remarks>
	/// Parameters are generated in the order of the descriptions they come from, so each description claims the first
	/// entry not already claimed by an earlier one. Names alone are not sufficient: two action parameters can generate
	/// entries sharing a name, and removing by name would then delete somebody else's parameter.
	/// </remarks>
	private static List<ParameterDescriptor?> MapParameterOwners(OpenApiOperation operation, ApiDescription description)
	{
		List<ParameterDescriptor?> owners = [.. Enumerable.Repeat<ParameterDescriptor?>(null, operation.Parameters!.Count)];
		bool[] claimed = new bool[operation.Parameters.Count];

		foreach (ApiParameterDescription parameterDescription in description.ParameterDescriptions)
		{
			for (int i = 0; i < operation.Parameters.Count; i++)
			{
				if (claimed[i] || operation.Parameters[i] is not OpenApiParameter candidate)
					continue;

				if (!string.Equals(candidate.Name, parameterDescription.Name, StringComparison.Ordinal))
					continue;

				claimed[i] = true;
				owners[i] = parameterDescription.ParameterDescriptor;

				break;
			}
		}

		return owners;
	}

	/// <summary>
	/// Replaces every parameter generated for one action parameter with a single parameter named after it.
	/// </summary>
	/// <param name="operation">The operation.</param>
	/// <param name="owners">The owning action parameter of each entry in <see cref="OpenApiOperation.Parameters"/>.</param>
	/// <param name="owner">The action parameter whose entries are being replaced.</param>
	/// <param name="parameter">The replacement parameter, when one could be produced.</param>
	/// <returns><see langword="true"/> when a replacement was made; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// When the parameter was not flattened this reuses the single existing entry. When it was flattened, every entry
	/// belonging to it is removed and one entry is inserted in its place, because the model binder reads the whole
	/// value from a single query string entry named after the action parameter.
	/// </remarks>
	private static bool TryReplaceParameters(OpenApiOperation operation, List<ParameterDescriptor?> owners, ParameterDescriptor owner, out OpenApiParameter parameter)
	{
		int index = -1;
		bool required = false;
		string? description = null;

		for (int i = operation.Parameters!.Count - 1; i >= 0; i--)
		{
			// Ownership rather than the name decides this, so a like named sibling parameter is never removed.
			if (!ReferenceEquals(owners[i], owner) || operation.Parameters[i] is not OpenApiParameter candidate)
				continue;

			index = i;
			required |= candidate.Required;

			// Only a parameter that was not flattened carries a description meant for the whole value.
			if (string.Equals(candidate.Name, owner.Name, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(candidate.Description))
				description = candidate.Description;

			operation.Parameters.RemoveAt(i);
			owners.RemoveAt(i);
		}

		if (index < 0)
		{
			parameter = null!;

			return false;
		}

		parameter = new OpenApiParameter
		{
			Name = owner.Name,
			In = ParameterLocation.Query,
			Required = required,
			Description = description
		};

		operation.Parameters.Insert(index, parameter);
		owners.Insert(index, owner);

		return true;
	}

	/// <summary>
	/// Replaces the schema of any enum property on the descriptor schema with a string schema listing the enum member
	/// names.
	/// </summary>
	/// <param name="descriptorSchema">The descriptor schema.</param>
	/// <param name="descriptorType">The descriptor type.</param>
	/// <remarks>
	/// Schema generation reflects how the application has configured enum serialization, which is commonly numeric. The
	/// model binders always deserialize using a
	/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>, so the names are documented instead. The
	/// match is performed without regard to case so that the common property naming policies are all handled.
	/// </remarks>
	private static void ApplyEnumMemberNames(OpenApiSchema descriptorSchema, Type descriptorType)
	{
		if (descriptorSchema.Properties is not { Count: > 0 })
			return;

		foreach (PropertyInfo property in descriptorType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

			if (!propertyType.IsEnum)
				continue;

			string? key = descriptorSchema.Properties.Keys.FirstOrDefault(x => string.Equals(x, property.Name, StringComparison.OrdinalIgnoreCase));

			if (key is null)
				continue;

			string[] memberNames = _enumMemberNameCache.GetOrAdd(propertyType, static x => Enum.GetNames(x));

			descriptorSchema.Properties[key] = new OpenApiSchema
			{
				Type = JsonSchemaType.String,
				Enum = [.. memberNames.Select(x => (JsonNode)x)]
			};
		}
	}

	/// <summary>
	/// Builds the fallback description applied when the action has not supplied one of its own.
	/// </summary>
	/// <param name="descriptorType">The descriptor type.</param>
	/// <param name="isCollection">Specifies whether the parameter accepts a collection.</param>
	/// <returns>The description.</returns>
	private static string BuildDescription(Type descriptorType, bool isCollection)
	{
		string noun = descriptorType == typeof(SortExpressionDescriptor) ? "sort" : "filter";

		return isCollection
			? $"A JSON array of {noun} expressions, sent as a single URL encoded query string value. A single object is also accepted."
			: $"A {noun} expression sent as a single URL encoded JSON query string value.";
	}
}
#endif
