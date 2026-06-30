using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Umbrella.AspNetCore.WebUtilities.Middleware;

/// <summary>
/// An <see cref="IStartupFilter"/> that registers <see cref="BrowserLinkNonceMiddleware"/> as the
/// outermost middleware in the pipeline. This ensures it wraps the BrowserLink and browser-refresh
/// startup-filter middlewares and can inject nonces after they have written their script tags.
/// </summary>
public sealed class BrowserLinkNonceStartupFilter : IStartupFilter
{
	/// <inheritdoc />
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
	{
		_ = app.UseMiddleware<BrowserLinkNonceMiddleware>();
		next(app);
	};
}
