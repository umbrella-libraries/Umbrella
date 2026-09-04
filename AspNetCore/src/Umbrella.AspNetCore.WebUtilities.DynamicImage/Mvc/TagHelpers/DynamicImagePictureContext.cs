using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

/// <summary>
/// The state shared between a <see cref="DynamicImageTagHelper"/> and the <see cref="DynamicImagePictureSourceTagHelper"/> instances nested inside it.
/// </summary>
/// <remarks>
/// An instance of this type is added to the <see cref="TagHelperContext.Items"/> dictionary by the parent tag helper before its child content is
/// executed. Child tag helpers resolve it from their own context, use it to inherit any values they have not explicitly declared, and append
/// their generated source tags to <see cref="Sources"/>.
/// </remarks>
public sealed class DynamicImagePictureContext
{
	private Task<string>? _parentSourcePathTask;

	/// <summary>
	/// Sets the delegate used to resolve the source path for a given tag helper context.
	/// </summary>
	/// <remarks>
	/// Resolution is deferred and asynchronous so that a source path that is only known at runtime, e.g. one obtained from a digital asset
	/// management system, can be produced by an overridden <see cref="DynamicImageTagHelper.ResolveSourcePathAsync"/>. The getter is private
	/// so that the delegate is only invoked through <see cref="ResolveParentSourcePathAsync"/> and <see cref="ResolveSourcePathAsync"/>,
	/// which decide what may be shared between elements.
	/// </remarks>
	public required Func<TagHelperContext, Task<string>> SourcePathResolver { private get; init; }

	/// <summary>
	/// Gets the <see cref="TagHelperContext"/> of the parent element.
	/// </summary>
	public required TagHelperContext ParentTagHelperContext { get; init; }

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

	/// <summary>
	/// Resolves the source path of the parent element.
	/// </summary>
	/// <returns>The source path with the configured prefix removed, or the URL unaltered when it is external.</returns>
	/// <remarks>
	/// The task is resolved once and returned to every subsequent caller, so a picture containing several nested sources that inherit the
	/// image resolves it once rather than once per source, which matters when resolution performs I/O.
	/// </remarks>
	public Task<string> ResolveParentSourcePathAsync() => _parentSourcePathTask ??= SourcePathResolver(ParentTagHelperContext);

	/// <summary>
	/// Resolves the source path of a nested source that declares a source of its own.
	/// </summary>
	/// <param name="context">The context of the nested source.</param>
	/// <returns>The source path with the configured prefix removed, or the URL unaltered when it is external.</returns>
	/// <remarks>
	/// This deliberately does not cache. Razor pools a <see cref="TagHelperContext"/> per nesting depth and reinitializes it in place for
	/// each sibling element, so the instance is not a stable identity for one element and using it as a cache key would serve the first
	/// sibling's path to every later one. Each source declaring its own image resolves it once here, which is the same number of resolutions
	/// as there are distinct images.
	/// </remarks>
	public Task<string> ResolveSourcePathAsync(TagHelperContext context)
	{
		Guard.IsNotNull(context);

		return SourcePathResolver(context);
	}
}
