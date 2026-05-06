using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

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

		DateTimeOffset? ifModifiedSince = request.GetTypedHeaders().IfModifiedSince;

		return ifModifiedSince.HasValue
			&& NormalizeHttpDate(valueToMatch) <= NormalizeHttpDate(ifModifiedSince.Value);
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

		string normalizedValueToMatch = RemoveWeakEtagPrefix(valueToMatch.Trim());

		return request.Headers.TryGetValue("If-None-Match", out StringValues values)
			&& values
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				.Select(RemoveWeakEtagPrefix)
				.Any(x => x == "*" || string.Equals(x, normalizedValueToMatch, StringComparison.Ordinal));
	}

	private static DateTimeOffset NormalizeHttpDate(DateTimeOffset value)
	{
		DateTimeOffset utcValue = value.ToUniversalTime();
		long ticks = utcValue.Ticks - (utcValue.Ticks % TimeSpan.TicksPerSecond);

		return new DateTimeOffset(ticks, TimeSpan.Zero);
	}

	private static string RemoveWeakEtagPrefix(string value) => value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
		? value[2..].Trim()
		: value;

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
