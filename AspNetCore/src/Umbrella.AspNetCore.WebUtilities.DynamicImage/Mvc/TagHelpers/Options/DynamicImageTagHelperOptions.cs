using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;

/// <summary>
/// Options for use with Dynamic Image Tag Helpers.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class DynamicImageTagHelperOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the dynamic image path prefix. Defaults to <see cref="DynamicImageConstants.DefaultPathPrefix"/>.
	/// </summary>
	public string DynamicImagePathPrefix { get; set; } = DynamicImageConstants.DefaultPathPrefix;

	/// <summary>
	/// Gets or sets the prefix to strip from the path before serving the image. Defaults to <see cref="UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName"/>.
	/// </summary>
	public string StripPrefix { get; set; } = "/" + UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName;

	/// <inheritdoc />
	public void Sanitize()
	{
		DynamicImagePathPrefix = DynamicImagePathPrefix.Trim();
		StripPrefix = StripPrefix.Trim();
	}

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNullOrWhiteSpace(DynamicImagePathPrefix);
		Guard.IsNotNullOrWhiteSpace(StripPrefix);

		if (!StripPrefix.StartsWith("/", StringComparison.Ordinal))
			throw new ArgumentException($"The {nameof(StripPrefix)} must start with a forward slash ('/').", nameof(StripPrefix));
	}
}
