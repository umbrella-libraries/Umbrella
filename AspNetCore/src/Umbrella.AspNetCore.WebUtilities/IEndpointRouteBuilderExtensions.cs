


using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Versioning;
using Umbrella.WebUtilities.Versioning.Abstractions;
using Umbrella.WebUtilities.Versioning.Models;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Extensions for use with the <see cref="IEndpointRouteBuilder"/> type.
/// </summary>
public static class IEndpointRouteBuilderExtensions
{
	/// <summary>
	/// Adds a <see cref="RouteEndpoint"/> to the <see cref="IEndpointRouteBuilder"/> that matches HTTP GET requests
	/// for the specified <paramref name="path"/> which returns version information about the current system.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="path">The path.</param>
	/// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
	public static RouteHandlerBuilder MapSystemVersionGet(this IEndpointRouteBuilder builder, string path = "/system-version")
		=> builder.MapGet(path, async (ILogger<SystemVersionService> logger, ISystemVersionService systemVersionService, CancellationToken cancellationToken) =>
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				SystemVersionModel model = await systemVersionService.GetAsync(cancellationToken).ConfigureAwait(false);

				return Results.Json(model);
			}
			catch (Exception exc) when (logger.WriteError(exc, new { path }))
			{
				return Results.Problem("There was a problem getting version information.", statusCode: 500);
			}
		});
}