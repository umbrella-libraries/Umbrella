using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Http.Extensions;

/// <summary>
/// Extension methods for the <see cref="HttpClient"/>.
/// </summary>
public static class HttpClientExtensions
{
	private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// Posts the specified <paramref name="value"/> as JSON to the specified <paramref name="url"/> with the Content-Length header
	/// of the request message calculated and set before sending the request.
	/// </summary>
	/// <typeparam name="T">The type of the content to be sent.</typeparam>
	/// <param name="httpClient">The HTTP client.</param>
	/// <param name="url">The URL.</param>
	/// <param name="value">The value.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The response.</returns>
	/// <remarks>
	/// This method exists to workaround an issue with the <c>PostAsJsonAsync</c> method which uses chunked tranfer encoding to send all
	/// messages regardless of whether or not the length of the content being sent is known in advance. Some servers do not accept requests
	/// using chunked transfer encoding. This method exists to workaround this issue.
	/// </remarks>
	public static async Task<HttpResponseMessage> PostAsJsonWithCalculatedContentLengthAsync<T>(this HttpClient httpClient, string url, T value, CancellationToken cancellationToken = default)
	{
		Guard.IsNotNull(httpClient);

		string json = JsonSerializer.Serialize(value, _options);

		using StringContent content = new(json, Encoding.UTF8, "application/json");
		content.Headers.ContentType!.CharSet = "utf-8";

		return await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
	}
}