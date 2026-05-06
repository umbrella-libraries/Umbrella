
using CommunityToolkit.Diagnostics;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.WebUtilities.FileSystem.Middleware.Options;

/// <summary>
/// Specifies a file provider mapping and options for that mapping for use with the file provider middleware.
/// </summary>
/// <seealso cref="IValidatableUmbrellaOptions" />
/// <seealso cref="ISanitizableUmbrellaOptions" />
public class FileSystemMiddlewareMapping : IValidatableUmbrellaOptions, ISanitizableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the cacheability. Defaults to <see cref="MiddlewareHttpCacheability.NoCache" />.
	/// </summary>
	public MiddlewareHttpCacheability Cacheability { get; set; } = MiddlewareHttpCacheability.NoCache;

	/// <summary>
	/// Gets or sets the optional max-age value, in seconds, for the Cache-Control header when
	/// <see cref="Cacheability"/> is <see cref="MiddlewareHttpCacheability.Private"/>.
	/// </summary>
	public int? MaxAgeSeconds { get; set; }

	/// <summary>
	/// Gets or sets the file provider mapping.
	/// </summary>
	public UmbrellaFileStorageProviderMapping FileProviderMapping { get; set; } = null!;

	/// <inheritdoc />
	public void Sanitize() => FileProviderMapping?.Sanitize();

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNull(FileProviderMapping);
		FileProviderMapping.Validate();

		if (Cacheability is MiddlewareHttpCacheability.Public)
			throw new ArgumentException("Public is not permitted.", nameof(Cacheability));

		if (Cacheability is not MiddlewareHttpCacheability.Private && MaxAgeSeconds.HasValue)
			throw new ArgumentException($"{nameof(MaxAgeSeconds)} can only be set when {nameof(Cacheability)} is {nameof(MiddlewareHttpCacheability.Private)}.", nameof(MaxAgeSeconds));

		if (MaxAgeSeconds < 0)
			throw new ArgumentException($"{nameof(MaxAgeSeconds)} cannot be less than zero.", nameof(MaxAgeSeconds));
	}
}
