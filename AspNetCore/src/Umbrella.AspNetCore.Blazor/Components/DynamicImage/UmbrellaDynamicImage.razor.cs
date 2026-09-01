using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.AspNetCore.Blazor.Components.ResponsiveImage;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Utilities.Imaging;

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage;

/// <summary>
/// A component used to render images in conjunction with the <see cref="Umbrella.DynamicImage"/> infrastructure.
/// </summary>
/// <remarks>
/// Nested <see cref="UmbrellaDynamicImageSource"/> components can be used to contribute art directed sources that are rendered before the
/// automatically generated format sources.
/// </remarks>
/// <seealso cref="UmbrellaResponsiveImage" />
public partial class UmbrellaDynamicImage : UmbrellaResponsiveImage
{
	/// <summary>
	/// Gets or set the dynamic image options.
	/// </summary>
	[Inject]
	protected UmbrellaDynamicImageOptions Options { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the dynamic image utility.
	/// </summary>
	[Inject]
	protected IDynamicImageUtility DynamicImageUtility { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the width request in pixels. Defaults to 1.
	/// </summary>
	[Parameter]
	public int WidthRequest { get; set; } = 1;

	/// <summary>
	/// Gets or sets the height request in pixels. Defaults to 1.
	/// </summary>
	[Parameter]
	public int HeightRequest { get; set; } = 1;

	/// <summary>
	/// Gets or sets the resize mode. Defaults to <see cref="DynamicResizeMode.Crop"/>.
	/// </summary>
	/// <remarks>
	/// For more information on how these resize modes work, please refer to the <see cref="DynamicResizeMode"/> code documentation.
	/// </remarks>
	[Parameter]
	public DynamicResizeMode ResizeMode { get; set; } = DynamicResizeMode.Crop;

	/// <summary>
	/// Gets or sets the image format. Defaults to <see cref="DynamicImageFormat.Jpeg"/>.
	/// </summary>
	[Parameter]
	public DynamicImageFormat ImageFormat { get; set; } = DynamicImageFormat.Jpeg;

	/// <summary>
	/// Gets or sets the normalised X coordinate of the focal point, between 0 and 1 starting from the left of the image.
	/// Only used with <see cref="DynamicResizeMode.CropFocalPoint"/>.
	/// </summary>
	[Parameter]
	public double? FocalPointX { get; set; }

	/// <summary>
	/// Gets or sets the normalised Y coordinate of the focal point, between 0 and 1 starting from the top of the image.
	/// Only used with <see cref="DynamicResizeMode.CropFocalPoint"/>.
	/// </summary>
	[Parameter]
	public double? FocalPointY { get; set; }

	/// <summary>
	/// Gets or sets the optional version token that should be embedded in generated Dynamic Image URLs.
	/// </summary>
	[Parameter]
	public string? VersionToken { get; set; }

	/// <summary>
	/// Gets or sets the size widths.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If specified, these are used in combination with the values of <see cref="UmbrellaResponsiveImage.MaxPixelDensity"/>,
	/// <see cref="WidthRequest"/> and <see cref="HeightRequest"/> to set the value of the srcset attribute on the rendered img tag.
	/// </para>
	/// <para>
	/// Please see the unit tests for <see cref="ResponsiveImageHelper.GetSizeSrcSetValue"/> for sample data.
	/// </para>
	/// </remarks>
	[Parameter]
	public string? SizeWidths { get; set; }

	/// <summary>
	/// Gets or sets the nested content, which can only contain <see cref="UmbrellaDynamicImageSource"/> components.
	/// </summary>
	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	/// <summary>
	/// Gets or sets the source value.
	/// </summary>
	protected string SrcValue { get; set; } = null!;

	/// <summary>
	/// Gets the picture sources rendered before the fallback image.
	/// </summary>
	protected IReadOnlyCollection<DynamicImagePictureSource> PictureSources { get; set; } = [];

	/// <summary>
	/// Gets the context cascaded to any nested <see cref="UmbrellaDynamicImageSource"/> components.
	/// </summary>
	protected UmbrellaDynamicImageContext? Context { get; set; }

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		Guard.IsNotNullOrWhiteSpace(Url, nameof(Url));
		Guard.IsGreaterThanOrEqualTo(WidthRequest, 1);
		Guard.IsGreaterThanOrEqualTo(HeightRequest, 1);

		InitializeImage();
	}

	/// <inheritdoc />
	protected override void InitializeImage()
	{
		if (Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			SrcValue = Url;
			SrcSetValue = ResponsiveImageHelper.GetPixelDensitySrcSetValue(SrcValue, MaxPixelDensity);
			PictureSources = [];
			Context = null;

			if (ChildContent is not null)
				throw new InvalidOperationException($"{nameof(UmbrellaDynamicImageSource)} components cannot be nested inside an {nameof(UmbrellaDynamicImage)} component that refers to an external URL.");

			return;
		}

		var context = new UmbrellaDynamicImageContext(Options, DynamicImageUtility, ResponsiveImageHelper, CreateSettings());
		DynamicImageSourceSettings settings = context.Settings;

		settings.ValidateFocalPoint();

		SrcValue = context.GenerateUrl(settings, settings.ImageFormat);
		SrcSetValue = context.GetSrcSetValue(settings, settings.ImageFormat);
		PictureSources = context.CreateSources(settings, media: null, includeFallbackFormat: false);
		Context = context;
	}

	/// <summary>
	/// Removes the configured prefix from the specified URL if it is present.
	/// </summary>
	/// <remarks>The prefix to remove is specified by the <see cref="UmbrellaDynamicImageOptions.StripPrefix"/> property. The comparison is
	/// case-insensitive. If the prefix is not set or not present at the start of the URL, the original URL is returned
	/// unchanged.</remarks>
	/// <param name="url">The URL string from which to remove the prefix. Cannot be null or empty.</param>
	/// <returns>A string containing the URL with the prefix removed if it was present; otherwise, the original URL.</returns>
	protected string StripUrlPrefix(string url)
	{
		Guard.IsNotNullOrEmpty(url);

		return !string.IsNullOrEmpty(Options.StripPrefix) && url.StartsWith(Options.StripPrefix, StringComparison.OrdinalIgnoreCase) ? url[Options.StripPrefix.Length..] : url;
	}

	private DynamicImageSourceSettings CreateSettings()
	{
		return new DynamicImageSourceSettings
		{
			// The prefix has to be stripped before the settings are built because nested sources inherit this URL as-is.
			Url = StripUrlPrefix(Url),
			WidthRequest = WidthRequest,
			HeightRequest = HeightRequest,
			ResizeMode = ResizeMode,
			ImageFormat = ImageFormat,
			MaxPixelDensity = MaxPixelDensity,
			SizeWidths = SizeWidths,
			FocalPointX = FocalPointX,
			FocalPointY = FocalPointY,
			VersionToken = VersionToken
		};
	}
}
