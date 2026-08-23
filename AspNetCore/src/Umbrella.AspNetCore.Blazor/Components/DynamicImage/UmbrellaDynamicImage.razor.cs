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
	/// Gets or sets the source value.
	/// </summary>
	protected string SrcValue { get; set; } = null!;

	/// <summary>
	/// Gets the picture sources rendered before the fallback image.
	/// </summary>
	protected IReadOnlyCollection<DynamicImagePictureSource> PictureSources { get; set; } = [];

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		Guard.IsNotNullOrWhiteSpace(Url, nameof(Url));
		Guard.IsGreaterThanOrEqualTo(WidthRequest, 1);
		Guard.IsGreaterThanOrEqualTo(HeightRequest, 1);
		ValidateFocalPoint();
	}

	/// <inheritdoc />
	protected override async Task OnParametersSetAsync()
	{
		await InitializeImageAsync();
	}

	private async Task InitializeImageAsync()
	{
		if (Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			SrcValue = Url;
			SrcSetValue = ResponsiveImageHelper.GetPixelDensitySrcSetValue(SrcValue, MaxPixelDensity);
			PictureSources = [];
			return;
		}

		string strippedUrl = StripUrlPrefix(Url);
		DynamicImageOptions options = CreateDynamicImageOptions(strippedUrl, WidthRequest, HeightRequest, ImageFormat);

		SrcValue = DynamicImageUtility.GenerateVirtualPath(Options.DynamicImagePathPrefix, options).TrimStart('~').Replace("//", "/", StringComparison.Ordinal);
		SrcSetValue = GetSrcSetValue(strippedUrl, ImageFormat, SrcValue);
		PictureSources =
		[
			.. Options.PictureSourceFormats
				.Where(x => x != ImageFormat)
				.Select(x =>
				{
					DynamicImageOptions sourceOptions = CreateDynamicImageOptions(strippedUrl, WidthRequest, HeightRequest, x);
					string sourceUrl = DynamicImageUtility.GenerateVirtualPath(Options.DynamicImagePathPrefix, sourceOptions).TrimStart('~').Replace("//", "/", StringComparison.Ordinal);
					return new DynamicImagePictureSource(x is DynamicImageFormat.Avif ? "image/avif" : "image/webp", GetSrcSetValue(strippedUrl, x, sourceUrl));
				})
		];
	}

	private string GetSrcSetValue(string sourcePath, DynamicImageFormat format, string src)
	{
		// TODO: Can't we just check for an empty string here or null? This parsing is done internally as well so it's a waste of time.
		IReadOnlyCollection<int> lstSizeWidth = ResponsiveImageHelper.GetParsedIntegerItems(SizeWidths ?? "");

		string? srcSet = lstSizeWidth.Count is 0
			? ResponsiveImageHelper.GetPixelDensitySrcSetValue(src, MaxPixelDensity)
			: ResponsiveImageHelper.GetSizeSrcSetValue(sourcePath, SizeWidths ?? "", MaxPixelDensity, WidthRequest, HeightRequest, x =>
			{
				DynamicImageOptions options = CreateDynamicImageOptions(sourcePath, x.imageWidth, x.imageHeight, format);

				return DynamicImageUtility.GenerateVirtualPath(Options.DynamicImagePathPrefix, options).TrimStart('~').Replace("//", "/", StringComparison.Ordinal);
			});

		return string.IsNullOrWhiteSpace(srcSet) ? src : srcSet;
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

	private DynamicImageOptions CreateDynamicImageOptions(string sourcePath, int width, int height, DynamicImageFormat format)
		=> new(
			sourcePath,
			width,
			height,
			ResizeMode,
			format,
			focalPointX: FocalPointX,
			focalPointY: FocalPointY,
			versionToken: VersionToken);

	private void ValidateFocalPoint()
	{
		if (FocalPointX.HasValue != FocalPointY.HasValue)
			throw new ArgumentException($"Both {nameof(FocalPointX)} and {nameof(FocalPointY)} must be defined if either is specified.");

		if (!FocalPointX.HasValue)
			return;

		Guard.IsBetweenOrEqualTo(FocalPointX.Value, 0, 1);
		Guard.IsBetweenOrEqualTo(FocalPointY!.Value, 0, 1);

		if (ResizeMode is not DynamicResizeMode.CropFocalPoint)
			throw new InvalidOperationException($"{nameof(FocalPointX)} and {nameof(FocalPointY)} can only be used with {nameof(DynamicResizeMode.CropFocalPoint)}.");
	}

	/// <summary>
	/// Describes a source rendered inside the picture element.
	/// </summary>
	/// <param name="ContentType">The source MIME type.</param>
	/// <param name="SrcSet">The responsive source set.</param>
	protected sealed record DynamicImagePictureSource(string ContentType, string SrcSet);
}
