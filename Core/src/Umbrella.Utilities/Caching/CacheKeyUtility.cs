


using System.Buffers;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Caching;

/// <summary>
/// A utility used to create cache keys.
/// </summary>
/// <seealso cref="ICacheKeyUtility" />
public class CacheKeyUtility : ICacheKeyUtility
{
	private readonly ILogger _log;
	private readonly IDataLookupNormalizer _lookupNormalizer;

	/// <summary>
	/// Initializes a new instance of the <see cref="CacheKeyUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	public CacheKeyUtility(
		ILogger<CacheKeyUtility> logger,
		IDataLookupNormalizer lookupNormalizer)
	{
		_log = logger;
		_lookupNormalizer = lookupNormalizer;
	}

	/// <inheritdoc />
	public string Create<T>(string key) => Create(typeof(T), key);

	/// <inheritdoc />
	public string Create(Type type, string key)
	{
		Guard.IsNotNull(type);
		Guard.IsNotNullOrWhiteSpace(key);

		char[]? rentedArray = null;

		try
		{
			string? typeName = type.FullName;

			Guard.IsNotNullOrEmpty(typeName);

			int length = typeName.Length + key.Length + 1;

			bool isStack = length <= StackAllocConstants.MaxCharSize;

			Span<char> span = isStack ? stackalloc char[length] : rentedArray = ArrayPool<char>.Shared.Rent(length);
			_ = span.Write(0, typeName);
			_ = span.Write(typeName.Length, ":");
			_ = span.Write(typeName.Length + 1, key);

			if (!isStack)
				span = span.Slice(0, length);

			return _lookupNormalizer.Normalize(span.ToString()) ?? throw new UmbrellaException("The span could not be normalized.");
		}
		catch (Exception exc) when (_log.WriteError(exc, new { type, key }))
		{
			throw new UmbrellaException("There was a problem creating the cache key.", exc);
		}
		finally
		{
			if (rentedArray is not null)
				ArrayPool<char>.Shared.Return(rentedArray);
		}
	}

	/// <inheritdoc />
	public string Create<T>(in ReadOnlySpan<string> keyParts, int? keyPartsLength = null) => Create(typeof(T), keyParts, keyPartsLength);

	/// <inheritdoc />
	public string Create(Type type, in ReadOnlySpan<string> keyParts, int? keyPartsLength = null)
	{
		Guard.IsNotNull(type);

		if (keyPartsLength is not null)
			Guard.IsBetweenOrEqualTo(keyPartsLength.Value, 1, keyParts.Length, nameof(keyPartsLength));

		char[]? rentedArray = null;

		try
		{
			int partsCount = keyPartsLength ?? keyParts.Length;

			// It seems the typeof call is expensive on CLR vs .NET Core
			string? typeName = type.FullName;

			Guard.IsNotNullOrEmpty(typeName);

			int partsLengthTotal = -1;

			for (int i = 0; i < partsCount; i++)
			{
				string part = keyParts[i];

				if (part is not null)
					partsLengthTotal += part.Length + 1;
			}

			int length = typeName.Length + partsLengthTotal + 1;

			bool isStack = length <= StackAllocConstants.MaxCharSize;

			Span<char> span = isStack ? stackalloc char[length] : rentedArray = ArrayPool<char>.Shared.Rent(length);

			int currentIndex = span.Write(0, typeName);
			span[currentIndex++] = ':';

			for (int i = 0; i < partsCount; i++)
			{
				string part = keyParts[i];

				if (part is not null)
				{
					currentIndex = span.Write(currentIndex, part);

					if (i < partsCount - 1)
						span[currentIndex++] = ':';
				}
			}

			if (!isStack)
				span = span.Slice(0, length);

			// This is the only part that allocates
			return _lookupNormalizer.Normalize(span.ToString()) ?? throw new UmbrellaException("The span could not be normalized.");
		}
		catch (Exception exc) when (_log.WriteError(exc, new { type, keyParts = keyParts.ToArray(), keyPartsLength }))
		{
			throw new UmbrellaException("There was a problem creating the cache key.", exc);
		}
		finally
		{
			if (rentedArray is not null)
				ArrayPool<char>.Shared.Return(rentedArray);
		}
	}

#if !AzureDevOps
	[Obsolete]
	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
	internal string CreateOld<T>(IEnumerable<string> keyParts) => $"{typeof(T).FullName}:{string.Join(":", keyParts)}".ToUpperInvariant();
#endif
}