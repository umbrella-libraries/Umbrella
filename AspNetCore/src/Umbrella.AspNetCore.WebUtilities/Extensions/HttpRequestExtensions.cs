


using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace Umbrella.AspNetCore.WebUtilities.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="HttpRequest"/> type.
/// </summary>
public static class HttpRequestExtensions
{
	/// <summary>
	/// Determines if the If-Modified-Since header matches the supplied <see cref="DateTimeOffset"/>.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="valueToMatch">The value to match.</param>
	/// <returns><see langword="true" /> if it can be matched, otherwise <see langword="false" /></returns>
	public static bool IfModifiedSinceHeaderMatched(this HttpRequest request, DateTimeOffset valueToMatch)
	{
		Guard.IsNotNull(request);

		string? ifModifiedSince = request.Headers.IfModifiedSince;

		if (!string.IsNullOrWhiteSpace(ifModifiedSince))
		{
			DateTime lastModified = DateTime.Parse(ifModifiedSince, CultureInfo.InvariantCulture).ToUniversalTime();

			return lastModified == valueToMatch;
		}

		return false;
	}

	/// <summary>
	/// Determines if the If-None-Match header matches the supplied value.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="valueToMatch">The value to match.</param>
	/// <returns><see langword="true" /> if it can be matched, otherwise <see langword="false" /></returns>
	public static bool IfNoneMatchHeaderMatched(this HttpRequest request, string valueToMatch)
	{
		Guard.IsNotNull(request);
		Guard.IsNotNullOrWhiteSpace(valueToMatch);

		string? ifNoneMatch = request.Headers.IfNoneMatch;

		return !string.IsNullOrWhiteSpace(ifNoneMatch) && string.Equals(ifNoneMatch, valueToMatch, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines if the client will accept webp image types.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns><see langword="true" /> if they are supported, otherwise <see langword="false" /></returns>
	public static bool AcceptsWebP(this HttpRequest request)
	{
		Guard.IsNotNull(request);

		return request.Headers.TryGetValue("Accept", out StringValues values)
			&& values.Any(x => !string.IsNullOrEmpty(x)
			&& x.Contains("image/webp", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Determines if the client will accept avif image types.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns><see langword="true" /> if they are supported, otherwise <see langword="false" /></returns>
	public static bool AcceptsAvif(this HttpRequest request)
	{
		Guard.IsNotNull(request);

		return request.Headers.TryGetValue("Accept", out StringValues values)
			&& values.Any(x => !string.IsNullOrEmpty(x)
			&& x.Contains("image/avif", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Determines whether the requesting client is IE by checking the User-Agent header to see if it contains
	/// the strings "MSIE" or "Trident" using ordinal case-insensitive comparison rules.
	/// </summary>
	/// <param name="request">The request.</param>
	public static bool IsInternetExplorer(this HttpRequest request)
	{
		Guard.IsNotNull(request);

		string? userAgent = request.Headers.UserAgent;

		return !string.IsNullOrWhiteSpace(userAgent) && (userAgent.Contains("MSIE", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Trident", StringComparison.OrdinalIgnoreCase));
	}
}