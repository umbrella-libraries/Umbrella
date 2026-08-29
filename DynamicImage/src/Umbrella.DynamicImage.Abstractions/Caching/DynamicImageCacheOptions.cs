using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.DynamicImage.Abstractions.Caching;

/// <summary>
/// Used to specify the core caching options. These options are only used for specifying caching durations for keys.
/// </summary>
/// <seealso cref="CacheableUmbrellaOptions" />
public class DynamicImageCacheCoreOptions : CacheableUmbrellaOptions, IValidatableUmbrellaOptions, ISanitizableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the name of the directory used to store cached images.
	/// </summary>
	/// <remarks>Defaults to <c>dynamicimagecache</c></remarks>
	public string DirectoryName { get; set; } = "dynamicimagecache";

	/// <inheritdoc />
	public void Sanitize() => DirectoryName = DirectoryName?.Trim()!;

	/// <inheritdoc />
	public void Validate() => Guard.IsNotNullOrWhiteSpace(DirectoryName);
}
