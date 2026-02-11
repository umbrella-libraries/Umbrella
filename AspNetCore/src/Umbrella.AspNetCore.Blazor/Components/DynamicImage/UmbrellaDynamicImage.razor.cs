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

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		Guard.IsGreaterThanOrEqualTo(WidthRequest, 1);
		Guard.IsGreaterThanOrEqualTo(HeightRequest, 1);
	}

	/// <inheritdoc />
	protected override void InitializeImage()
	{
		if (Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			SrcValue = Url;
			return;
		}

		string strippedUrl = StripUrlPrefix(Url);

		var options = new DynamicImageOptions(strippedUrl, WidthRequest, HeightRequest, ResizeMode, ImageFormat);

		SrcValue = DynamicImageUtility.GenerateVirtualPath(Options.DynamicImagePathPrefix, options).TrimStart('~').Replace("//", "/", StringComparison.Ordinal);

		// TODO: Can't we just check for an empty string here or null? This parsing is done internally as well so it's a waste of time.
		IReadOnlyCollection<int> lstSizeWidth = ResponsiveImageHelper.GetParsedIntegerItems(SizeWidths ?? "");

		SrcSetValue = lstSizeWidth.Count is 0
			? ResponsiveImageHelper.GetPixelDensitySrcSetValue(SrcValue, MaxPixelDensity)
			: ResponsiveImageHelper.GetSizeSrcSetValue(strippedUrl, SizeWidths ?? "", MaxPixelDensity, WidthRequest, HeightRequest, x =>
			{
				var options = new DynamicImageOptions(strippedUrl, x.imageWidth, x.imageHeight, ResizeMode, ImageFormat);

				return DynamicImageUtility.GenerateVirtualPath(Options.DynamicImagePathPrefix, options).TrimStart('~').Replace("//", "/", StringComparison.Ordinal);
			});
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
}