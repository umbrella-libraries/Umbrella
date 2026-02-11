


using System.Collections.Specialized;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extensions for use with the <see cref="NameValueCollection"/> type.
/// </summary>
public static class NameValueCollectionExtensions
{
	/// <summary>
	/// Serializes the <see cref="NameValueCollection"/> to a <see cref="string"/>.
	/// </summary>
	/// <param name="nvc">The <see cref="NameValueCollection"/>.</param>
	/// <param name="keyValueSeparator">The key value separator.</param>
	/// <param name="pairSeparator">The pair separator.</param>
	/// <returns>The <see cref="NameValueCollection"/> serialized to a <see cref="string"/>.</returns>
	public static string SerializeToString(this NameValueCollection nvc, string keyValueSeparator = "=", string pairSeparator = "&")
		=> SerializeToString(nvc, WebUtility.UrlEncode, keyValueSeparator, pairSeparator);

	/// <summary>
	/// Serializes the <see cref="NameValueCollection"/> to a <see cref="string"/> using the specified <paramref name="urlEncoder"/>
	/// to transform each value contained within the <see cref="NameValueCollection"/>.
	/// </summary>
	/// <param name="nvc">The <see cref="NameValueCollection"/>.</param>
	/// <param name="urlEncoder">The URL encoder.</param>
	/// <param name="keyValueSeparator">The key value separator.</param>
	/// <param name="pairSeparator">The pair separator.</param>
	/// <returns>The <see cref="NameValueCollection"/> serialized to a <see cref="string"/>.</returns>
	public static string SerializeToString(this NameValueCollection nvc, Func<string, string> urlEncoder, string keyValueSeparator = "=", string pairSeparator = "&")
	{
		Guard.IsNotNull(nvc, nameof(nvc));
		Guard.IsNotNull(urlEncoder, nameof(urlEncoder));
		Guard.IsNotNullOrWhiteSpace(keyValueSeparator, nameof(keyValueSeparator));
		Guard.IsNotNullOrWhiteSpace(pairSeparator, nameof(pairSeparator));

		return string.Join(pairSeparator,
					nvc.AllKeys.Where(key => !string.IsNullOrEmpty(key) && !string.IsNullOrWhiteSpace(nvc[key]))
						.Select(
							key => string.Join(pairSeparator, nvc.GetValues(key!)?.Select(val => $"{urlEncoder(key!)}{keyValueSeparator}{urlEncoder(val)}") ?? Array.Empty<string>())));
	}
}