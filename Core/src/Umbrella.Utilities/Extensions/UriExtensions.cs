using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Http;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods that operate on <see cref="Uri"/> instances.
/// </summary>
public static class UriExtensions
{
	/// <summary>
	/// Tries to get the query string value from the <see cref="Uri.Query"/> property.
	/// </summary>
	/// <typeparam name="T">The type of the value being read from the query string.</typeparam>
	/// <param name="uri">The URI.</param>
	/// <param name="key">The key of the value being read from the query string.</param>
	/// <returns>A tuple containing fields indicating success together with any value.</returns>
	/// <exception cref="NotSupportedException">Query parameters of type {typeof(T).Name} cannot be converted.</exception>
	public static (bool success, T value) TryGetQueryStringValue<T>(this Uri uri, string key)
	{
		if (QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var valueFromQueryString))
		{
			if (typeof(T) == typeof(int) && int.TryParse(valueFromQueryString, out int valueAsInt))
			{
				var value = (T)(object)valueAsInt;
				return (true, value);
			}

			if (typeof(T) == typeof(string))
			{
				var value = (T)(object)valueFromQueryString.ToString();
				return (true, value);
			}

			if (typeof(T) == typeof(double) && double.TryParse(valueFromQueryString, out double valueAsDouble))
			{
				var value = (T)(object)valueAsDouble;
				return (true, value);
			}

			if (typeof(T) == typeof(bool) && bool.TryParse(valueFromQueryString, out bool valueAsBool))
			{
				var value = (T)(object)valueAsBool;
				return (true, value);
			}

			throw new NotSupportedException($"Query parameters of type {typeof(T).Name} cannot be converted.");
		}

		return default;
	}

	/// <summary>
	/// Tries to get the query string enum value from the <see cref="Uri.Query"/> property.
	/// </summary>
	/// <typeparam name="T">The type of the enum value being read from the query string.</typeparam>
	/// <param name="uri">The URI.</param>
	/// <param name="key">The key of the value being read from the query string.</param>
	/// <returns>A tuple containing fields indicating success together with any enum value.</returns>
	public static (bool success, T value) TryGetQueryStringEnumValue<T>(this Uri uri, string key)
		where T : struct, Enum => QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var valueFromQueryString) && Enum.TryParse<T>(valueFromQueryString, true, out var valueAsEnum)
			? (true, valueAsEnum)
			: default;

	/// <summary>
	/// Strips a Uri of the specified query string parameters.
	/// </summary>
	/// <param name="uri">The URI.</param>
	/// <param name="keysToRemove">The keys of the query string parameters to remove.</param>
	/// <returns>Returns the input <see cref="Uri"/> with the specified query string parameters removed.</returns>
	public static Uri GetUriWithoutQueryStringParameters(this Uri uri, string[] keysToRemove)
	{
		Guard.IsNotNull(uri, nameof(uri));
		Guard.IsNotNull(keysToRemove, nameof(keysToRemove));

		// Parse the query string into a name-value collection
		var queryParameters = QueryHelpers.ParseQuery(uri.Query);

		// Remove the specified keys from the collection
		foreach (string key in keysToRemove)
		{
			_ = queryParameters.Remove(key);
		}

		// Rebuild the query string from the remaining parameters
		string newQueryString = string.Join("&", queryParameters.Keys
			.Where(key => !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(queryParameters[key]))
			.Select(key => $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(queryParameters[key]!)}"));

		// Reconstruct the URI without the removed query parameters
		var uriBuilder = new UriBuilder(uri)
		{
			Query = newQueryString
		};

		return uriBuilder.Uri;
	}
}