
using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Middleware;
using Umbrella.DynamicImage.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods used to register Middleware for the <see cref="Umbrella.AspNetCore.WebUtilities.DynamicImage"/> package with a specified <see cref="IApplicationBuilder"/>.
/// </summary>
public static class IApplicationBuilderExtensions
{
	/// <summary>
	/// Adds the <see cref="DynamicImageMiddleware"/> to the pipeline.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="webFolderPath">The path, relative to the website root, that dynamic images are served from. Defaults to <see cref="DynamicImageConstants.DefaultPathPrefix" /> with a leading forward slash.</param>
	/// <returns>The application builder.</returns>
	/// <remarks>
	/// Dynamic Image URLs must take the following format: /{prefix}/{width}/{height}/{resizeMode}/{originalExtension}/{originalExtensionlessPath}.{targetExtension},
	/// e.g. from an original URL of /images/image.jpg, the following URL will be dynamically resize the image to a .png: /dynamicimages/200/150/UniformFill/jpg/images/image.png.
	/// <see cref="IDynamicImageUtility.GenerateVirtualPath(string, DynamicImageOptions)"/> can be used to generate application relative image URLs.
	/// </remarks>
	public static IApplicationBuilder UseUmbrellaDynamicImage(this IApplicationBuilder builder, string webFolderPath = "/" + DynamicImageConstants.DefaultPathPrefix)
	{
		Guard.IsNotNull(builder);

		_ = builder.MapWhen(context => context.Request.Path.StartsWithSegments(webFolderPath, StringComparison.OrdinalIgnoreCase), app => app.UseMiddleware<DynamicImageMiddleware>());

		return builder;
	}
}