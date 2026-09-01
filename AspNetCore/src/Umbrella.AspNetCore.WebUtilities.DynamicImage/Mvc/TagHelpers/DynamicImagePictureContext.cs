using Microsoft.AspNetCore.Html;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

/// <summary>
/// The state shared between a <see cref="DynamicImageTagHelper"/> and the <see cref="DynamicImagePictureSourceTagHelper"/> instances nested inside it.
/// </summary>
/// <remarks>
/// An instance of this type is added to the <see cref="Microsoft.AspNetCore.Razor.TagHelpers.TagHelperContext.Items"/> dictionary by the parent
/// tag helper before its child content is executed. Child tag helpers resolve it from their own context, use it to inherit any values they have not
/// explicitly declared, and append their generated source tags to <see cref="Sources"/>.
/// </remarks>
public sealed class DynamicImagePictureContext
{
	/// <summary>
	/// Gets the source path of the parent image with the configured prefix already stripped.
	/// </summary>
	public required string SourcePath { get; init; }

	/// <summary>
	/// Gets a value indicating whether the parent image refers to an external URL.
	/// </summary>
	public required bool IsExternalUrl { get; init; }

	/// <summary>
	/// Gets the version token of the parent image.
	/// </summary>
	public required string? VersionToken { get; init; }

	/// <summary>
	/// Gets the resize mode of the parent image.
	/// </summary>
	public required DynamicResizeMode ResizeMode { get; init; }

	/// <summary>
	/// Gets the image format of the parent image.
	/// </summary>
	public required DynamicImageFormat ImageFormat { get; init; }

	/// <summary>
	/// Gets the filter quality of the parent image.
	/// </summary>
	public required DynamicImageFilterQuality FilterQuality { get; init; }

	/// <summary>
	/// Gets the quality request of the parent image.
	/// </summary>
	public required int QualityRequest { get; init; }

	/// <summary>
	/// Gets the maximum pixel density of the parent image.
	/// </summary>
	public required int MaxPixelDensity { get; init; }

	/// <summary>
	/// Gets the size widths of the parent image.
	/// </summary>
	public required string? SizeWidths { get; init; }

	/// <summary>
	/// Gets the normalised X coordinate of the focal point of the parent image.
	/// </summary>
	public required double? FocalPointX { get; init; }

	/// <summary>
	/// Gets the normalised Y coordinate of the focal point of the parent image.
	/// </summary>
	public required double? FocalPointY { get; init; }

	/// <summary>
	/// Gets the source tags contributed by nested <see cref="DynamicImagePictureSourceTagHelper"/> instances, in declaration order.
	/// </summary>
	public List<IHtmlContent> Sources { get; } = [];
}
