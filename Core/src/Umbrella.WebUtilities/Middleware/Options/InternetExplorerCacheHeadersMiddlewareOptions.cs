
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.WebUtilities.Middleware.Options;

/// <summary>
/// Options for implementations of the InternetExplorerCacheHeaderMiddleware in the ASP.NET and ASP.NET Core projects.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class InternetExplorerCacheHeadersMiddlewareOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Keywords to search the User-Agent header value for.
	/// </summary>
	public List<string> UserAgentKeywords { get; set; } = ["MSIE", "Trident"];

	/// <summary>
	/// HTTP Methods that the middleware will act on.
	/// </summary>
	public List<string> Methods { get; set; } = ["GET", "HEAD"];

	/// <summary>
	/// Content-Type header values that the middleware will act on.
	/// </summary>
	public List<string> ContentTypes { get; set; } = ["application/json"];

	/// <summary>
	/// Sanitizes this instance.
	/// </summary>
	public void Sanitize()
	{
		UserAgentKeywords = UserAgentKeywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		Methods = Methods.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		ContentTypes = ContentTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
	}

	/// <summary>
	/// Validates this instance.
	/// </summary>
	public void Validate()
	{
		Guard.IsNotNull(UserAgentKeywords);
		Guard.IsNotNull(Methods);
		Guard.IsNotNull(ContentTypes);
		Guard.HasSizeGreaterThan(UserAgentKeywords, 0);
		Guard.HasSizeGreaterThan(Methods, 0);
		Guard.HasSizeGreaterThan(ContentTypes, 0);
	}
}