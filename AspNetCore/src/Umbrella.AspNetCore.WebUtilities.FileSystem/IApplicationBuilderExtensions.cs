
using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.WebUtilities.FileSystem.Middleware;
using Umbrella.FileSystem.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods used to register Middleware for the <see cref="Umbrella.AspNetCore.WebUtilities.FileSystem"/> package with a specified <see cref="IApplicationBuilder"/>.
/// </summary>
public static class IApplicationBuilderExtensions
{
	/// <summary>
	/// Add the <see cref="FileSystemMiddleware"/> to the pipeline.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// /// <param name="webFolderPath">The path, relative to the website root, that dynamic images are served from. Defaults to <see cref="UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName" /> with a leading forward slash.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaFileSystem(this IApplicationBuilder builder, string webFolderPath = "/" + UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName)
	{
		Guard.IsNotNull(builder);

		_ = builder.MapWhen(context => context.Request.Path.StartsWithSegments(webFolderPath, StringComparison.OrdinalIgnoreCase), app => app.UseMiddleware<FileSystemMiddleware>());

		return builder;
	}
}