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
	/// Gets or sets a value indicating whether generated dynamic image URLs should include a fingerprint/version token.
	/// Defaults to <see langword="true" />.
	/// </summary>
	public bool EnableUrlFingerprinting { get; set; } = true;

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
			throw new ArgumentException($"The {nameof(StripPrefix)} must start with a forward slash '/'.", nameof(StripPrefix));
	}
}
