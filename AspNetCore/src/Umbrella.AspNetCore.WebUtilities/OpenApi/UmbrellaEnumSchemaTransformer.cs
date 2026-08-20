#if NET10_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Completes the schema generated for an enum so that it states which values are permitted.
/// </summary>
/// <remarks>
/// <para>
/// Schema generation describes an enum only by its underlying kind, for example <c>{ "type": "integer" }</c>, leaving a
/// consumer with no way of discovering which values are valid. This transformer adds the permitted values, taking the
/// form they are given from the application's own JSON configuration rather than assuming one: the enum is serialized
/// using the same options the payloads will be, and the result decides whether names or numbers are documented.
/// Registering a <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> therefore updates the document
/// automatically, and removing it updates the document back.
/// </para>
/// <para>
/// Enums bound from the query string, the route or a header do not use System.Text.Json at all, so they are documented
/// by <see cref="UmbrellaEnumParameterOperationTransformer"/> instead, which overwrites the parameter schema.
/// </para>
/// </remarks>
internal sealed class UmbrellaEnumSchemaTransformer : IOpenApiSchemaTransformer
{
	/// <inheritdoc />
	public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(schema);
		Guard.IsNotNull(context);

		Type type = context.JsonTypeInfo.Type;
		type = Nullable.GetUnderlyingType(type) ?? type;

		if (!type.IsEnum || schema.Enum is { Count: > 0 })
			return Task.CompletedTask;

		IReadOnlyList<object> values = EnumSchemaHelper.GetDistinctValues(type);

		if (values.Count is 0)
			return Task.CompletedTask;

		JsonTypeInfo typeInfo;

		try
		{
			typeInfo = ResolveSerializerOptions(context).GetTypeInfo(type);
		}
		catch (Exception exc) when (exc is InvalidOperationException or NotSupportedException)
		{
			// The options cannot describe the enum, so there is nothing trustworthy to document.
			return Task.CompletedTask;
		}

		List<JsonNode> members = [];
		HashSet<string> seen = new(StringComparer.Ordinal);
		bool asString = false;

		foreach (object value in values)
		{
			JsonNode? node;

			try
			{
				node = JsonNode.Parse(JsonSerializer.Serialize(value, typeInfo));
			}
			catch (Exception exc) when (exc is JsonException or NotSupportedException)
			{
				return Task.CompletedTask;
			}

			if (node is null)
				return Task.CompletedTask;

			asString |= node.GetValueKind() is JsonValueKind.String;

			// A converter may map several members onto one representation, which must not be listed twice.
			if (seen.Add(node.ToJsonString()))
				members.Add(node);
		}

		schema.Type = asString ? JsonSchemaType.String : JsonSchemaType.Integer;

		// Flag combinations are legal without being declared members, so the values are described rather than enforced.
		if (!EnumSchemaHelper.IsFlags(type))
			schema.Enum = members;

		if (!asString || EnumSchemaHelper.IsFlags(type))
			schema.Description = EnumSchemaHelper.BuildDescription(schema.Description, type, asString);

		return Task.CompletedTask;
	}

	/// <summary>
	/// Resolves the JSON options that will actually be used to serialize the payloads this document describes.
	/// </summary>
	/// <param name="context">The context.</param>
	/// <returns>The options.</returns>
	/// <remarks>
	/// The OpenAPI schema generator reads <see cref="Microsoft.AspNetCore.Http.Json.JsonOptions"/>, which is correct for
	/// minimal APIs but is not what a controller uses. Controllers serialize using
	/// <see cref="Microsoft.AspNetCore.Mvc.JsonOptions"/>, configured through <c>AddJsonOptions</c>, and the generator
	/// never consults it. Those options are preferred here whenever MVC is present so that the document reflects the
	/// payloads the application will really produce and accept. An application hosting both controllers and minimal
	/// APIs whose two sets of options disagree will have its minimal API enums described using the MVC options.
	/// </remarks>
	private static JsonSerializerOptions ResolveSerializerOptions(OpenApiSchemaTransformerContext context)
	{
		IServiceProvider services = context.ApplicationServices;

		// Resolving IOptions always succeeds, so the presence of MVC itself is what decides which options apply.
		if (services.GetService<IActionDescriptorCollectionProvider>() is not null
			&& services.GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()?.Value.JsonSerializerOptions is { } mvcOptions)
		{
			return mvcOptions;
		}

		return services.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions
			?? context.JsonTypeInfo.Options;
	}
}
#endif
