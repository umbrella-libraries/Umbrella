#if NET10_0_OR_GREATER
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Removes component schemas that nothing in the document refers to.
/// </summary>
/// <remarks>
/// <para>
/// Schemas are collected while the operations are being generated, before any operation transformer has had the chance
/// to replace them. A transformer that rewrites a parameter or response therefore leaves the schemas it displaced
/// behind in <see cref="OpenApiComponents.Schemas"/>, where documentation UIs continue to list them. The most visible
/// example is <see cref="UmbrellaDataExpressionParameterOperationTransformer"/>, which displaces the
/// <see cref="System.Linq.Expressions.Expression"/> tree schemas generated for the Data Expression types.
/// </para>
/// <para>
/// Reachability is calculated from every part of the document other than <see cref="OpenApiComponents.Schemas"/>
/// itself, so a schema that is still referenced from a path, a webhook or any other reusable component is retained. In
/// a generated document a schema only exists because something referenced it, so anything left unreachable is dead
/// weight.
/// </para>
/// </remarks>
public sealed class UmbrellaUnreferencedSchemaDocumentTransformer : IOpenApiDocumentTransformer
{
	/// <inheritdoc/>
	public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(document);

		IDictionary<string, IOpenApiSchema>? schemas = document.Components?.Schemas;

		if (schemas is not { Count: > 0 })
			return Task.CompletedTask;

		HashSet<string> reachable = new(StringComparer.Ordinal);

		VisitPathItems(document.Paths, schemas, reachable);
		VisitPathItems(document.Webhooks, schemas, reachable);
		VisitComponents(document.Components, schemas, reachable);

		foreach (string key in schemas.Keys.Where(x => !reachable.Contains(x)).ToArray())
		{
			_ = schemas.Remove(key);
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Visits a collection of path items.
	/// </summary>
	/// <param name="pathItems">The path items.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitPathItems(IDictionary<string, IOpenApiPathItem>? pathItems, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (pathItems is null)
			return;

		foreach (IOpenApiPathItem pathItem in pathItems.Values)
		{
			VisitPathItem(pathItem, schemas, reachable);
		}
	}

	/// <summary>
	/// Visits a single path item together with each of its operations.
	/// </summary>
	/// <param name="pathItem">The path item.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitPathItem(IOpenApiPathItem? pathItem, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (pathItem is null)
			return;

		VisitParameters(pathItem.Parameters, schemas, reachable);

		if (pathItem.Operations is null)
			return;

		foreach (OpenApiOperation operation in pathItem.Operations.Values)
		{
			VisitParameters(operation.Parameters, schemas, reachable);
			VisitContent(operation.RequestBody?.Content, schemas, reachable);

			if (operation.Responses is not null)
			{
				foreach (IOpenApiResponse response in operation.Responses.Values)
				{
					VisitResponse(response, schemas, reachable);
				}
			}

			if (operation.Callbacks is null)
				continue;

			foreach (IOpenApiCallback callback in operation.Callbacks.Values)
			{
				VisitCallback(callback, schemas, reachable);
			}
		}
	}

	/// <summary>
	/// Visits every reusable component other than the schemas themselves.
	/// </summary>
	/// <param name="components">The components.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitComponents(OpenApiComponents? components, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (components is null)
			return;

		VisitParameters(components.Parameters?.Values.ToArray(), schemas, reachable);

		if (components.RequestBodies is not null)
		{
			foreach (IOpenApiRequestBody requestBody in components.RequestBodies.Values)
			{
				VisitContent(requestBody.Content, schemas, reachable);
			}
		}

		if (components.Responses is not null)
		{
			foreach (IOpenApiResponse response in components.Responses.Values)
			{
				VisitResponse(response, schemas, reachable);
			}
		}

		if (components.Headers is not null)
		{
			foreach (IOpenApiHeader header in components.Headers.Values)
			{
				VisitHeader(header, schemas, reachable);
			}
		}

		if (components.PathItems is not null)
		{
			foreach (IOpenApiPathItem pathItem in components.PathItems.Values)
			{
				VisitPathItem(pathItem, schemas, reachable);
			}
		}

		if (components.Callbacks is null)
			return;

		foreach (IOpenApiCallback callback in components.Callbacks.Values)
		{
			VisitCallback(callback, schemas, reachable);
		}
	}

	/// <summary>
	/// Visits every path item belonging to a callback.
	/// </summary>
	/// <param name="callback">The callback.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitCallback(IOpenApiCallback? callback, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (callback?.PathItems is null)
			return;

		foreach (IOpenApiPathItem pathItem in callback.PathItems.Values)
		{
			VisitPathItem(pathItem, schemas, reachable);
		}
	}

	/// <summary>
	/// Visits a response, covering both its content and its headers.
	/// </summary>
	/// <param name="response">The response.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitResponse(IOpenApiResponse? response, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (response is null)
			return;

		VisitContent(response.Content, schemas, reachable);

		if (response.Headers is null)
			return;

		foreach (IOpenApiHeader header in response.Headers.Values)
		{
			VisitHeader(header, schemas, reachable);
		}
	}

	/// <summary>
	/// Visits a header, covering both its schema and its content.
	/// </summary>
	/// <param name="header">The header.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitHeader(IOpenApiHeader? header, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (header is null)
			return;

		VisitSchema(header.Schema, schemas, reachable);
		VisitContent(header.Content, schemas, reachable);
	}

	/// <summary>
	/// Visits a collection of parameters, covering both their schemas and their content.
	/// </summary>
	/// <param name="parameters">The parameters.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitParameters(IEnumerable<IOpenApiParameter>? parameters, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (parameters is null)
			return;

		foreach (IOpenApiParameter parameter in parameters)
		{
			VisitSchema(parameter.Schema, schemas, reachable);
			VisitContent(parameter.Content, schemas, reachable);
		}
	}

	/// <summary>
	/// Visits the schemas of each media type in a content dictionary.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitContent(IDictionary<string, OpenApiMediaType>? content, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (content is null)
			return;

		foreach (OpenApiMediaType mediaType in content.Values)
		{
			VisitSchema(mediaType.Schema, schemas, reachable);
		}
	}

	/// <summary>
	/// Visits a schema, recording it when it is a reference and recursing through every subschema it contains.
	/// </summary>
	/// <param name="schema">The schema.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	/// <remarks>
	/// A referenced schema is only followed the first time it is seen, which both bounds the work and makes the walk
	/// safe for the recursive schemas produced by self referencing models.
	/// </remarks>
	private static void VisitSchema(IOpenApiSchema? schema, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (schema is null)
			return;

		if (schema is OpenApiSchemaReference reference)
		{
			string? id = reference.Reference?.Id;

			if (id is null || !reachable.Add(id))
				return;

			if (schemas.TryGetValue(id, out IOpenApiSchema? target))
				VisitSchema(target, schemas, reachable);

			return;
		}

		VisitSchema(schema.Items, schemas, reachable);
		VisitSchema(schema.Not, schemas, reachable);
		VisitSchema(schema.AdditionalProperties, schemas, reachable);

		VisitSchemas(schema.AllOf, schemas, reachable);
		VisitSchemas(schema.AnyOf, schemas, reachable);
		VisitSchemas(schema.OneOf, schemas, reachable);

		VisitSchemas(schema.Properties?.Values, schemas, reachable);
		VisitSchemas(schema.PatternProperties?.Values, schemas, reachable);
		VisitSchemas(schema.Definitions?.Values, schemas, reachable);
	}

	/// <summary>
	/// Visits a collection of subschemas.
	/// </summary>
	/// <param name="items">The subschemas.</param>
	/// <param name="schemas">The component schemas.</param>
	/// <param name="reachable">The set of reachable schema identifiers.</param>
	private static void VisitSchemas(IEnumerable<IOpenApiSchema>? items, IDictionary<string, IOpenApiSchema> schemas, HashSet<string> reachable)
	{
		if (items is null)
			return;

		foreach (IOpenApiSchema item in items)
		{
			VisitSchema(item, schemas, reachable);
		}
	}
}
#endif
