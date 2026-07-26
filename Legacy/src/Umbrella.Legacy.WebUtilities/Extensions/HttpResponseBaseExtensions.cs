using System.Web;

namespace Umbrella.Legacy.WebUtilities.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="HttpResponseBase"/> type.
/// </summary>
public static class HttpResponseBaseExtensions
{
	/// <summary>
	/// Determine whether the <see cref="HttpResponseBase"/> has a success code between 200 and 299 inclusive.
	/// </summary>
	/// <param name="response">The <see cref="HttpResponseBase"/> to check.</param>
	/// <returns><see langword="true"/> if the response status code was between 200 and 299 inclusive. Otherwise <see langword="false"/>.</returns>
	public static bool IsSuccessStatusCode(this HttpResponseBase response)
	{
		if (response is null)
			throw new ArgumentNullException(nameof(response));

		return response.StatusCode >= 200 && response.StatusCode <= 299;
	}
}
