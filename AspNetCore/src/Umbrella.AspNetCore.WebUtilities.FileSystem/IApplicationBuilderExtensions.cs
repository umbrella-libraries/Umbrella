
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.AspNetCore.WebUtilities.FileSystem.Middleware;
using Umbrella.WebUtilities.FileSystem.Middleware.Options;

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
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaFileSystem(this IApplicationBuilder builder)
	{
		Guard.IsNotNull(builder);

		FileSystemMiddlewareOptions options = builder.ApplicationServices.GetRequiredService<FileSystemMiddlewareOptions>();
		string fileSystemPathPrefix = "/" + options.FileSystemPathPrefix.Trim('/');

		_ = builder.MapWhen(
			context => context.Request.Path.StartsWithSegments(fileSystemPathPrefix, StringComparison.OrdinalIgnoreCase, out PathString remainingPath)
				&& remainingPath.HasValue
				&& remainingPath.Value.Length > 1,
			app => app.UseMiddleware<FileSystemMiddleware>());

		return builder;
	}
}