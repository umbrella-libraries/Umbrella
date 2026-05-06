
using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.WebUtilities.DynamicImage.Middleware.Options;

/// <summary>
/// Specifies a file provider mapping and options for that mapping for use with the dynamic image middleware.
/// </summary>
/// <seealso cref="IValidatableUmbrellaOptions" />
/// <seealso cref="ISanitizableUmbrellaOptions" />
public class DynamicImageMiddlewareMapping : IValidatableUmbrellaOptions, ISanitizableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether validation of the dynamic images being generated is performed.
	/// This is to ensure that only specific pre-determined options can be provided as part of the incoming URLs for the images
	/// to prevent overloading the server. Defaults to <see langword="false" />.
	/// </summary>
	public bool EnableValidation { get; set; }

	/// <summary>
	/// Gets or sets the valid mappings.
	/// </summary>
	public HashSet<DynamicImageMapping> ValidMappings { get; set; } = [];

	/// <summary>
	/// Gets or sets the cacheability. Defaults to <see cref="MiddlewareHttpCacheability.NoCache" />.
	/// </summary>
	public MiddlewareHttpCacheability Cacheability { get; set; } = MiddlewareHttpCacheability.NoCache;

	/// <summary>
	/// Gets or sets the optional max-age value, in seconds, for the Cache-Control header when
	/// <see cref="Cacheability"/> is <see cref="MiddlewareHttpCacheability.Private"/> or
	/// <see cref="MiddlewareHttpCacheability.Public"/>.
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

		if (Cacheability is not MiddlewareHttpCacheability.Private
			and not MiddlewareHttpCacheability.Public
			&& MaxAgeSeconds.HasValue)
		{
			throw new ArgumentException($"{nameof(MaxAgeSeconds)} can only be set when {nameof(Cacheability)} is {nameof(MiddlewareHttpCacheability.Private)} or {nameof(MiddlewareHttpCacheability.Public)}.", nameof(MaxAgeSeconds));
		}

		if (MaxAgeSeconds < 0)
			throw new ArgumentException($"{nameof(MaxAgeSeconds)} cannot be less than zero.", nameof(MaxAgeSeconds));

		if (EnableValidation)
		{
			Guard.IsNotNull(ValidMappings);
			Guard.IsGreaterThan(ValidMappings.Count, 0);
		}
	}
}
