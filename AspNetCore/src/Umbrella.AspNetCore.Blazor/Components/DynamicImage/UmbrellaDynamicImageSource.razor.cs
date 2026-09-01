using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage;

/// <summary>
/// A component used to contribute art directed sources to the picture element rendered by a parent <see cref="UmbrellaDynamicImage"/>
/// component.
/// </summary>
/// <remarks>
/// <para>
/// This must be nested inside an <see cref="UmbrellaDynamicImage"/> component. Each usage renders one source per configured
/// <see cref="UmbrellaDynamicImageOptions.PictureSourceFormats"/> value together with a final source using its own <see cref="ImageFormat"/>.
/// The own-format source is required because a browser that has matched a media condition will not fall back to the img element when it does
/// not support any of the formats offered for that condition.
/// </para>
/// <para>
/// Every parameter is nullable, and a parameter that has not been set is inherited from the parent component.
/// </para>
/// </remarks>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaDynamicImageSource : ComponentBase
{
	/// <summary>
	/// Gets or sets the context cascaded by the parent <see cref="UmbrellaDynamicImage"/> component.
	/// </summary>
	[CascadingParameter]
	protected UmbrellaDynamicImageContext? Context { get; set; }

	/// <summary>
	/// Gets or sets the media condition that the generated sources apply to, e.g. <c>(max-width: 599px)</c>.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public string Media { get; set; } = null!;

	/// <summary>
	/// Gets or sets the URL. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public string? Url { get; set; }

	/// <summary>
	/// Gets or sets the width request in pixels. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public int? WidthRequest { get; set; }

	/// <summary>
	/// Gets or sets the height request in pixels. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public int? HeightRequest { get; set; }

	/// <summary>
	/// Gets or sets the resize mode. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public DynamicResizeMode? ResizeMode { get; set; }

	/// <summary>
	/// Gets or sets the image format used for the fallback source. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public DynamicImageFormat? ImageFormat { get; set; }

	/// <summary>
	/// Gets or sets the maximum pixel density. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public int? MaxPixelDensity { get; set; }

	/// <summary>
	/// Gets or sets the size widths. Inherited from the parent component when not specified.
	/// </summary>
	[Parameter]
	public string? SizeWidths { get; set; }

	/// <summary>
	/// Gets or sets the normalised X coordinate of the focal point. Inherited from the parent component when not specified and the effective
	/// resize mode is <see cref="DynamicResizeMode.CropFocalPoint"/>.
	/// </summary>
	[Parameter]
	public double? FocalPointX { get; set; }

	/// <summary>
	/// Gets or sets the normalised Y coordinate of the focal point. Inherited from the parent component when not specified and the effective
	/// resize mode is <see cref="DynamicResizeMode.CropFocalPoint"/>.
	/// </summary>
	[Parameter]
	public double? FocalPointY { get; set; }

	/// <summary>
	/// Gets or sets the optional version token that should be embedded in generated Dynamic Image URLs. Inherited from the parent component
	/// when not specified.
	/// </summary>
	[Parameter]
	public string? VersionToken { get; set; }

	/// <summary>
	/// Gets or sets the additional attributes applied to every source rendered by this component, e.g. <c>sizes</c>.
	/// </summary>
	/// <remarks>
	/// A <c>sizes</c> value matters whenever <see cref="SizeWidths"/> is used: without one the browser assumes <c>100vw</c> and can pick a
	/// larger candidate than the layout needs. An <c>id</c> is discarded rather than repeated across the generated sources.
	/// </remarks>
	[Parameter(CaptureUnmatchedValues = true)]
	public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

	/// <summary>
	/// Gets the sources rendered by this component.
	/// </summary>
	protected IReadOnlyCollection<DynamicImagePictureSource> PictureSources { get; set; } = [];

	/// <summary>
	/// Gets the attributes applied to each generated source, which are the additional attributes without any <c>id</c>.
	/// </summary>
	protected IReadOnlyDictionary<string, object>? SourceAttributes { get; private set; }

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		if (Context is null)
			throw new InvalidOperationException($"An {nameof(UmbrellaDynamicImageSource)} component can only be used inside an {nameof(UmbrellaDynamicImage)} component.");

		Guard.IsNotNullOrWhiteSpace(Media);

		DynamicImageSourceSettings settings = CreateSettings(Context.Settings);
		settings.ValidateFocalPoint();

		PictureSources = Context.CreateSources(settings, Media, includeFallbackFormat: true);
		SourceAttributes = BuildSourceAttributes();
	}

	/// <summary>
	/// Removes any <c>id</c> from the additional attributes.
	/// </summary>
	/// <returns>The attributes to apply to each generated source.</returns>
	/// <remarks>
	/// This component renders several source tags, so splatting an id onto all of them would repeat it and produce invalid HTML. The
	/// <c>dynamic-source</c> tag helper excludes it in the same way.
	/// </remarks>
	private IReadOnlyDictionary<string, object>? BuildSourceAttributes()
	{
		if (AdditionalAttributes is null || !AdditionalAttributes.Keys.Any(x => string.Equals(x, "id", StringComparison.OrdinalIgnoreCase)))
			return AdditionalAttributes;

		return AdditionalAttributes
			.Where(x => !string.Equals(x.Key, "id", StringComparison.OrdinalIgnoreCase))
			.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
	}

	private DynamicImageSourceSettings CreateSettings(DynamicImageSourceSettings inherited)
	{
		DynamicResizeMode resizeMode = ResizeMode ?? inherited.ResizeMode;

		// A focal point is only meaningful for the CropFocalPoint resize mode, so it is only inherited when the effective resize mode still
		// calls for one. Without this, specifying a different resize mode on a child of a focal point parent would throw.
		bool inheritFocalPoint = resizeMode is DynamicResizeMode.CropFocalPoint;

		return new DynamicImageSourceSettings
		{
			Url = Url is null ? inherited.Url : Context!.StripUrlPrefix(Url),
			WidthRequest = WidthRequest ?? inherited.WidthRequest,
			HeightRequest = HeightRequest ?? inherited.HeightRequest,
			ResizeMode = resizeMode,
			ImageFormat = ImageFormat ?? inherited.ImageFormat,
			MaxPixelDensity = MaxPixelDensity ?? inherited.MaxPixelDensity,
			SizeWidths = SizeWidths ?? inherited.SizeWidths,
			FocalPointX = FocalPointX ?? (inheritFocalPoint ? inherited.FocalPointX : null),
			FocalPointY = FocalPointY ?? (inheritFocalPoint ? inherited.FocalPointY : null),
			VersionToken = VersionToken ?? inherited.VersionToken
		};
	}
}
