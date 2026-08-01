using System.Security.Cryptography;
using CommunityToolkit.Diagnostics;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Creates stable version tokens for files served by Umbrella file-system and Dynamic Image components.
/// </summary>
public static class UmbrellaFileVersionTokenUtility
{
	/// <summary>
	/// Creates a version token from file metadata.
	/// </summary>
	public static string Create(DateTimeOffset lastModified, long contentLength)
		=> Convert.ToString(lastModified.UtcDateTime.ToFileTimeUtc() ^ contentLength, 16);

	/// <summary>
	/// Creates a version token for an existing file, preferring metadata and falling back to a content hash.
	/// </summary>
	public static async Task<string?> CreateAsync(IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		if (fileInfo.LastModified.HasValue)
			return Create(fileInfo.LastModified.Value, fileInfo.Length);

		if (fileInfo.IsNew)
			return null;

		if (!await fileInfo.ExistsAsync(cancellationToken).ConfigureAwait(false))
			return null;

		using var hasher = SHA256.Create();
		using Stream sourceStream = await fileInfo.ReadAsStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		using var hashStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write);

		await sourceStream.CopyToAsync(hashStream, 81920, cancellationToken).ConfigureAwait(false);
#if NET8_0_OR_GREATER
		await hashStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
		return Convert.ToHexString(hasher.Hash!).ToLowerInvariant();
#else
		hashStream.FlushFinalBlock();
		return BitConverter.ToString(hasher.Hash!).Replace("-", string.Empty).ToLowerInvariant();
#endif
	}
}
