using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;

/// <summary>
/// Options for use with the <see cref="UmbrellaDynamicImage" /> component.
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class UmbrellaDynamicImageOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the dynamic image path prefix. Defaults to <see cref="DynamicImageConstants.DefaultPathPrefix"/>.
	/// </summary>
	public string DynamicImagePathPrefix { get; set; } = DynamicImageConstants.DefaultPathPrefix;

	/// <summary>
	/// Gets or sets the prefix to strip from the path before serving the image. Defaults to <see cref="UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName"/>.
	/// </summary>
	public string StripPrefix { get; set; } = "/" + UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName;

	/// <summary>
	/// Gets or sets the ordered formats rendered as picture sources before the fallback image.
	/// Defaults to <see cref="DynamicImageFormat.WebP" />.
	/// </summary>
	public List<DynamicImageFormat> PictureSourceFormats { get; set; } = [DynamicImageFormat.WebP];

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
		Guard.IsNotNull(PictureSourceFormats);

		if (!StripPrefix.StartsWith("/", StringComparison.Ordinal))
			throw new ArgumentException($"The {nameof(StripPrefix)} must start with a forward slash '/'.", nameof(StripPrefix));

		if (PictureSourceFormats.Any(x => x is not DynamicImageFormat.Avif and not DynamicImageFormat.WebP))
			throw new ArgumentException($"The {nameof(PictureSourceFormats)} collection can only contain AVIF and WebP formats.", nameof(PictureSourceFormats));

		if (PictureSourceFormats.Distinct().Count() != PictureSourceFormats.Count)
			throw new ArgumentException($"The {nameof(PictureSourceFormats)} collection cannot contain duplicate formats.", nameof(PictureSourceFormats));
	}
}
