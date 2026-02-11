


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Encodings.Web;

namespace Umbrella.Utilities.Http;

/// <summary>
/// Provides methods for parsing and manipulating query strings.
/// </summary>
/// <remarks>
/// This class has been copied from the Microsoft.AspNetCore.WebUtilities project on GitHub because
/// Microsoft no longer release this as a separate NuGet package we can reference here. It is now
/// part of the shared framework only.
/// </remarks>
public static class QueryHelpers
{
	/// <summary>
	/// Append the given query key and value to the URI.
	/// </summary>
	/// <param name="uri">The base URI.</param>
	/// <param name="name">The name of the query key.</param>
	/// <param name="value">The query value.</param>
	/// <returns>The combined result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="uri"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
	public static string AddQueryString(string uri, string name, string value)
	{
		Guard.IsNotNull(uri);
		Guard.IsNotNull(name);
		Guard.IsNotNull(value);

		return AddQueryString(uri, new[] { new KeyValuePair<string, string>(name, value) });
	}

	/// <summary>
	/// Append the given query keys and values to the URI.
	/// </summary>
	/// <param name="uri">The base URI.</param>
	/// <param name="queryString">A dictionary of query keys and values to append.</param>
	/// <returns>The combined result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="uri"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="queryString"/> is <c>null</c>.</exception>
	public static string AddQueryString(string uri, IDictionary<string, string> queryString)
	{
		Guard.IsNotNull(uri);
		Guard.IsNotNull(queryString);

		return AddQueryString(uri, (IEnumerable<KeyValuePair<string, string>>)queryString);
	}

	/// <summary>
	/// Append the given query keys and values to the URI.
	/// </summary>
	/// <param name="uri">The base URI.</param>
	/// <param name="queryString">A collection of query names and values to append.</param>
	/// <returns>The combined result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="uri"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="queryString"/> is <c>null</c>.</exception>
	public static string AddQueryString(string uri, IEnumerable<KeyValuePair<string, StringValues>> queryString)
	{
		Guard.IsNotNull(uri);
		Guard.IsNotNull(queryString);

		return AddQueryString(uri, queryString.SelectMany(kvp => kvp.Value, (kvp, v) => new KeyValuePair<string, string>(kvp.Key, v ?? "")));
	}

	/// <summary>
	/// Append the given query keys and values to the URI.
	/// </summary>
	/// <param name="uri">The base URI.</param>
	/// <param name="queryString">A collection of name value query pairs to append.</param>
	/// <returns>The combined result.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="uri"/> is <c>null</c>.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="queryString"/> is <c>null</c>.</exception>
	public static string AddQueryString(
		string uri,
		IEnumerable<KeyValuePair<string, string>> queryString)
	{
		Guard.IsNotNull(uri);
		Guard.IsNotNull(queryString);

#if NET6_0_OR_GREATER
		int anchorIndex = uri.IndexOf('#', StringComparison.Ordinal);
#else
		int anchorIndex = uri.IndexOf('#');
#endif
		string uriToBeAppended = uri;
		string anchorText = "";
		// If there is an anchor, then the query string must be inserted before its first occurrence.
		if (anchorIndex != -1)
		{
			anchorText = uri[anchorIndex..];
			uriToBeAppended = uri[..anchorIndex];
		}

#if NET6_0_OR_GREATER
		int queryIndex = uriToBeAppended.IndexOf('?', StringComparison.Ordinal);
#else
		int queryIndex = uriToBeAppended.IndexOf('?');
#endif
		bool hasQuery = queryIndex != -1;

		var sb = new StringBuilder();
		_ = sb.Append(uriToBeAppended);
		foreach (var parameter in queryString)
		{
			if (parameter.Value is null)
			{
				continue;
			}

			_ = sb.Append(hasQuery ? '&' : '?');
			_ = sb.Append(UrlEncoder.Default.Encode(parameter.Key));
			_ = sb.Append('=');
			_ = sb.Append(UrlEncoder.Default.Encode(parameter.Value));
			hasQuery = true;
		}

		_ = sb.Append(anchorText);
		return sb.ToString();
	}

	/// <summary>
	/// Parse a query string into its component key and value parts.
	/// </summary>
	/// <param name="queryString">The raw query string value, with or without the leading '?'.</param>
	/// <returns>A collection of parsed keys and values.</returns>
	public static Dictionary<string, StringValues> ParseQuery(string queryString) => ParseNullableQuery(queryString) ?? [];

	/// <summary>
	/// Parse a query string into its component key and value parts.
	/// </summary>
	/// <param name="queryString">The raw query string value, with or without the leading '?'.</param>
	/// <returns>A collection of parsed keys and values, null if there are no entries.</returns>
	public static Dictionary<string, StringValues>? ParseNullableQuery(string queryString)
	{
		var accumulator = new KeyValueAccumulator();

		if (string.IsNullOrEmpty(queryString) || queryString == "?")
		{
			return null;
		}

		int scanIndex = 0;
		if (queryString[0] == '?')
		{
			scanIndex = 1;
		}

		int textLength = queryString.Length;

#if NET6_0_OR_GREATER
		int equalIndex = queryString.IndexOf('=', StringComparison.Ordinal);
#else
		int equalIndex = queryString.IndexOf('=');
#endif

		if (equalIndex == -1)
		{
			equalIndex = textLength;
		}

		while (scanIndex < textLength)
		{
			int delimiterIndex = queryString.IndexOf('&', scanIndex);
			if (delimiterIndex == -1)
			{
				delimiterIndex = textLength;
			}

			if (equalIndex < delimiterIndex)
			{
				while (scanIndex != equalIndex && char.IsWhiteSpace(queryString[scanIndex]))
				{
					++scanIndex;
				}

				string name = queryString[scanIndex..equalIndex];
				string value = queryString.Substring(equalIndex + 1, delimiterIndex - equalIndex - 1);
				accumulator.Append(
					Uri.UnescapeDataString(name.Replace('+', ' ')),
					Uri.UnescapeDataString(value.Replace('+', ' ')));
				equalIndex = queryString.IndexOf('=', delimiterIndex);
				if (equalIndex == -1)
				{
					equalIndex = textLength;
				}
			}
			else
			{
				if (delimiterIndex > scanIndex)
				{
					accumulator.Append(queryString[scanIndex..delimiterIndex], string.Empty);
				}
			}

			scanIndex = delimiterIndex + 1;
		}

		if (!accumulator.HasValues)
		{
			return null;
		}

		return accumulator.GetResults();
	}
}

internal struct KeyValueAccumulator
{
	private Dictionary<string, StringValues> _accumulator;
	private Dictionary<string, List<string>> _expandingAccumulator;

	public void Append(string key, string value)
	{
		_accumulator ??= new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

		if (_accumulator.TryGetValue(key, out StringValues values))
		{
			if (values.Count == 0)
			{
				// Marker entry for this key to indicate entry already in expanding list dictionary
				_expandingAccumulator[key].Add(value);
			}
			else if (values.Count == 1)
			{
				// Second value for this key
				_accumulator[key] = new string?[] { values[0], value };
			}
			else
			{
				// Third value for this key
				// Add zero count entry and move to data to expanding list dictionary
				_accumulator[key] = default;

				_expandingAccumulator ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

				// Already 3 entries so use starting allocated as 8; then use List's expansion mechanism for more
				var list = new List<string>(8);
				string?[] array = values.ToArray();

				list.Add(array[0] ?? "");
				list.Add(array[1] ?? "");
				list.Add(value);

				_expandingAccumulator[key] = list;
			}
		}
		else
		{
			// First value for this key
			_accumulator[key] = new StringValues(value);
		}

		ValueCount++;
	}

	public readonly bool HasValues => ValueCount > 0;

	public readonly int KeyCount => _accumulator?.Count ?? 0;

	public int ValueCount { get; private set; }

	public readonly Dictionary<string, StringValues> GetResults()
	{
		if (_expandingAccumulator is not null)
		{
			// Coalesce count 3+ multi-value entries into _accumulator dictionary
			foreach (var entry in _expandingAccumulator)
			{
				_accumulator[entry.Key] = new StringValues(entry.Value.ToArray());
			}
		}

		return _accumulator ?? new Dictionary<string, StringValues>(0, StringComparer.OrdinalIgnoreCase);
	}
}