


using System.Security.Claims;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.Utilities.Http;

namespace Umbrella.AppFramework.Shared.Security.Extensions;

/// <summary>
/// Extension methods for the <see cref="string"/> type.
/// </summary>
public static class StringExtensions
{
	/// <summary>
	/// Appends the file access token stored as a claim of the specified <paramref name="user"/>
	/// as a querystring parameter to the specified <paramref name="url"/>.
	/// </summary>
	/// <param name="url">The URL to append the token to.</param>
	/// <param name="user">The user whose claims will be searched for the token.</param>
	/// <returns>The modified URL containing the file access token.</returns>
	/// <remarks>If the token cannot be found or has no value, the value of the <paramref name="url"/> parameter will be returned.</remarks>
	public static string? AppendFileAccessTokenIfAvailable(this string? url, ClaimsPrincipal? user)
	{
		if (url is null || user is null)
			return url;

		string? fat = user.GetFileAccessToken();

		return fat is null ? url : QueryHelpers.AddQueryString(url, AppQueryStringKeys.FileAccessToken, fat);
	}
}