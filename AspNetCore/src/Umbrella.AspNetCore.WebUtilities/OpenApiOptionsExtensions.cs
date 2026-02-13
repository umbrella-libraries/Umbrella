#if NET10_0_OR_GREATER
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Umbrella.AspNetCore.WebUtilities.OpenApi;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for the <see cref="OpenApiOptions"/> type, providing methods to add Umbrella's custom OpenAPI document
/// and operation transformers.
/// </summary>
public static class OpenApiOptionsExtensions
{
	extension(OpenApiOptions options)
	{
		/// <summary>
		/// Adds Umbrella's custom OpenAPI document transformers to the specified OpenAPI options.
		/// </summary>
		/// <returns>The same instance of the <see cref="OpenApiOptions"/>, enabling method chaining.</returns>
		public OpenApiOptions AddUmbrellaOpenApiDocumentTransformers()
		{
			Guard.IsNotNull(options);

			_ = options.AddDocumentTransformer<UmbrellaControllerDescriptionTagDocumentTransformer>();
			_ = options.AddDocumentTransformer<UmbrellaSecuritySchemesDocumentTransformer>();
			_ = options.AddOperationTransformer<UmbrellaAuthorizeOperationSecurityTransformer>();

			return options;
		}
	}
}
#endif