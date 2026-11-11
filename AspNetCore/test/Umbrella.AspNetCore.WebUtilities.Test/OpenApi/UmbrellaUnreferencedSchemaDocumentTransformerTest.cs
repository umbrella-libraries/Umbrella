using System.Net.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Moq;
using Umbrella.AspNetCore.WebUtilities.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.Test.OpenApi;

public sealed class UmbrellaUnreferencedSchemaDocumentTransformerTest
{
	[Fact]
	public async Task Ordinary_response_schemas_retain_transitive_and_recursive_references()
	{
		OpenApiDocument document = CreateDocument();
		AddResponse(document, new OpenApiMediaType { Schema = new OpenApiSchemaReference("Payload", document) });

		await PruneAsync(document);

		AssertRetainedSchemas(document);
	}

#if NET11_0_OR_GREATER
	[Theory]
	[InlineData(0, false)]
	[InlineData(0, true)]
	[InlineData(1, false)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	[InlineData(2, true)]
	public async Task ItemSchema_retains_payload_schemas_and_prunes_unreachable_schemas(int location, bool inlineArray)
	{
		OpenApiDocument document = CreateDocument();
		var reference = new OpenApiSchemaReference("Payload", document);
		var mediaType = new OpenApiMediaType
		{
			ItemSchema = inlineArray ? new OpenApiSchema { Type = JsonSchemaType.Array, Items = reference } : reference
		};

		switch (location)
		{
			case 0:
				AddResponse(document, mediaType);
				break;
			case 1:
				document.Components!.RequestBodies = new Dictionary<string, IOpenApiRequestBody>
				{
					["StreamRequest"] = new OpenApiRequestBody
					{
						Content = new Dictionary<string, IOpenApiMediaType> { ["application/json-seq"] = mediaType }
					}
				};
				break;
			default:
				document.Components!.MediaTypes = new Dictionary<string, IOpenApiMediaType> { ["Stream"] = mediaType };
				break;
		}

		Assert.Null(mediaType.Schema);

		await PruneAsync(document);

		AssertRetainedSchemas(document);
	}
#endif

	private static OpenApiDocument CreateDocument()
	{
		var document = new OpenApiDocument
		{
			Components = new OpenApiComponents
			{
				Schemas = new Dictionary<string, IOpenApiSchema>
				{
					["Payload"] = new OpenApiSchema { Type = JsonSchemaType.Object },
					["Detail"] = new OpenApiSchema { Type = JsonSchemaType.Object },
					["Unreachable"] = new OpenApiSchema { Type = JsonSchemaType.Object }
				}
			},
			Paths = []
		};
		((OpenApiSchema)document.Components.Schemas["Payload"]).Properties = new Dictionary<string, IOpenApiSchema>
		{
			["detail"] = new OpenApiSchemaReference("Detail", document)
		};
		((OpenApiSchema)document.Components.Schemas["Detail"]).Properties = new Dictionary<string, IOpenApiSchema>
		{
			["parent"] = new OpenApiSchemaReference("Payload", document)
		};
		return document;
	}

	private static void AddResponse(OpenApiDocument document, OpenApiMediaType mediaType)
	{
		document.Paths["/events"] = new OpenApiPathItem
		{
			Operations = new Dictionary<HttpMethod, OpenApiOperation>
			{
				[HttpMethod.Get] = new OpenApiOperation
				{
					Responses = new OpenApiResponses
					{
						["200"] = new OpenApiResponse
						{
							Description = "Event stream",
#if NET11_0_OR_GREATER
							Content = new Dictionary<string, IOpenApiMediaType> { ["text/event-stream"] = mediaType }
#else
							Content = new Dictionary<string, OpenApiMediaType> { ["text/event-stream"] = mediaType }
#endif
						}
					}
				}
			}
		};
	}

	private static async Task PruneAsync(OpenApiDocument document)
	{
		var context = new OpenApiDocumentTransformerContext
		{
			DocumentName = "v1",
			DescriptionGroups = [],
			ApplicationServices = Mock.Of<IServiceProvider>()
		};
		await new UmbrellaUnreferencedSchemaDocumentTransformer().TransformAsync(document, context, TestContext.Current.CancellationToken);
	}

	private static void AssertRetainedSchemas(OpenApiDocument document)
	{
		Assert.Equal(["Detail", "Payload"], document.Components!.Schemas!.Keys.Order(StringComparer.Ordinal));
	}
}
