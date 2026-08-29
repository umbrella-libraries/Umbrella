using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Options;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.Middleware.Options.LinkHeader;

/// <summary>
/// Options for use with associated middleware in ASP.NET and ASP.NET Core projects.
/// </summary>
/// <seealso cref="CacheableUmbrellaOptions" />
public class LinkHeaderMiddlewareOptions : CacheableUmbrellaOptions, ISanitizableUmbrellaOptions
{
	/// <summary>
	/// The URLs to be output as <c>Link</c> headers with rel=dns-prefetch and rel=preconnect values.
	/// </summary>
	public HashSet<LinkHeaderDnsPrefetchPreconnectUrl> DnsPrefetchPreconnectUrls { get; } = [];

	/// <summary>
	/// The URLs to be output as <c>Link</c> headers with rel=preconnect values.
	/// </summary>
	public HashSet<LinkHeaderPreloadUrl> PreloadUrls { get; } = [];

	/// <summary>
	/// Specifies whether the ingestion endpoints for Application Insights services
	/// should be included.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="true" />
	/// </remarks>
	public bool AddApplicationInsightsEndpoints { get; set; } = true;

	/// <inheritdoc/>
	public void Sanitize()
	{
		HashSet<LinkHeaderDnsPrefetchPreconnectUrl> lstSanitizedDnsPrefetchPreconnectUrl = [.. DnsPrefetchPreconnectUrls.Select(x => x with { Url = x.Url.TrimToLowerInvariant() })];

		if (AddApplicationInsightsEndpoints)
		{
			_ = lstSanitizedDnsPrefetchPreconnectUrl.Add(new("https://js.monitor.azure.com"));
			_ = lstSanitizedDnsPrefetchPreconnectUrl.Add(new("https://dc.services.visualstudio.com"));
		}

		DnsPrefetchPreconnectUrls.Clear();
		DnsPrefetchPreconnectUrls.AddRange(lstSanitizedDnsPrefetchPreconnectUrl);

		HashSet<LinkHeaderPreloadUrl> lstSanitizedPreloadUrl = [.. PreloadUrls.Select(x => x with { Url = x.Url.TrimToLowerInvariant() })];

		PreloadUrls.Clear();
		PreloadUrls.AddRange(lstSanitizedPreloadUrl);
	}
}
