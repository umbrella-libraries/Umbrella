#if NET10_0_OR_GREATER
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Documents enum typed query, route and header parameters using their member names.
/// </summary>
/// <remarks>
/// <para>
/// Enums bound from the query string, the route or a header go through the MVC model binder rather than
/// System.Text.Json. That binder parses the member name without regard to case and also accepts the underlying numeric
/// value, rejecting anything that is not a defined member. The names are therefore always valid, whatever the
/// application has configured for JSON enum serialization, and are far more useful to a consumer than the numbers that
/// schema generation would otherwise produce.
/// </para>
/// <para>
/// The schema is written directly onto the parameter rather than onto the shared component schema for the enum. An
/// enum used both as a parameter and within a request body would otherwise end up with a single component describing
/// only one of the two, and the body form is not necessarily the same. Bodies are handled separately by
/// <see cref="UmbrellaEnumSchemaTransformer"/>.
/// </para>
/// </remarks>
internal sealed class UmbrellaEnumParameterOperationTransformer : IOpenApiOperationTransformer
{
	private static readonly ConcurrentDictionary<Type, string[]> _enumMemberNameCache = new();

	/// <inheritdoc />
	public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(operation);
		Guard.IsNotNull(context);

		if (operation.Parameters is not { Count: > 0 })
			return Task.CompletedTask;

		foreach (ApiParameterDescription parameterDescription in context.Description.ParameterDescriptions)
		{
			Type? modelType = parameterDescription.ModelMetadata?.UnderlyingOrModelType;

			if (modelType is null)
				continue;

			var (enumType, isCollection) = GetEnumTypeData(modelType);

			if (enumType is null)
				continue;

			OpenApiParameter? parameter = null;

			foreach (IOpenApiParameter item in operation.Parameters)
			{
				// Referenced parameters are shared with other operations so are deliberately left alone.
				if (item is OpenApiParameter candidate && string.Equals(candidate.Name, parameterDescription.Name, StringComparison.Ordinal))
				{
					parameter = candidate;
					break;
				}
			}

			// A parameter already described using content holds a JSON document rather than a bare value.
			if (parameter is null || parameter.Content is { Count: > 0 })
				continue;

			OpenApiSchema memberSchema = CreateMemberSchema(enumType);

			parameter.Schema = isCollection
				? new OpenApiSchema
				{
					Type = JsonSchemaType.Array,
					Items = memberSchema
				}
				: memberSchema;

			if (string.IsNullOrWhiteSpace(parameter.Description))
			{
				parameter.Description = EnumSchemaHelper.IsFlags(enumType)
					? EnumSchemaHelper.BuildDescription("Supply a comma separated list to combine values. The member names are matched without regard to case and the underlying numeric value is also accepted.", enumType, asString: true)
					: "The member name is matched without regard to case. The underlying numeric value is also accepted.";
			}
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Resolves the enum type behind a parameter, which may be the parameter type itself or the element type of a
	/// collection.
	/// </summary>
	/// <param name="modelType">The model type of the parameter.</param>
	/// <returns>
	/// The enum type together with a value specifying whether the parameter accepts a collection, or
	/// <see langword="null"/> when the parameter is not an enum.
	/// </returns>
	private static (Type? EnumType, bool IsCollection) GetEnumTypeData(Type modelType)
	{
		if (modelType.IsEnum)
			return (modelType, false);

		var (isEnumerable, elementType) = modelType.GetIEnumerableTypeData();

		if (!isEnumerable || elementType is null)
			return (null, false);

		elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

		return elementType.IsEnum ? (elementType, true) : (null, false);
	}

	/// <summary>
	/// Creates the schema describing a single enum value.
	/// </summary>
	/// <param name="enumType">The enum type.</param>
	/// <returns>The schema.</returns>
	/// <remarks>
	/// A flags enum is left unconstrained because the binder accepts combinations that are not declared members, so an
	/// <c>enum</c> keyword would document legal input as invalid.
	/// </remarks>
	private static OpenApiSchema CreateMemberSchema(Type enumType)
	{
		if (EnumSchemaHelper.IsFlags(enumType))
			return new OpenApiSchema { Type = JsonSchemaType.String };

		string[] memberNames = _enumMemberNameCache.GetOrAdd(
			enumType,
			static x => [.. EnumSchemaHelper.GetDistinctValues(x).Select(v => Enum.GetName(x, v)).OfType<string>()]);

		return new OpenApiSchema
		{
			Type = JsonSchemaType.String,
			Enum = [.. memberNames.Select(x => (JsonNode)x)]
		};
	}
}
#endif
