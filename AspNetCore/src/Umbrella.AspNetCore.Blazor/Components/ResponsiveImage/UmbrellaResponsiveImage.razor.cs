using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.Utilities.Imaging.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.ResponsiveImage;

/// <summary>
/// A component used to render responsive images.
/// </summary>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaResponsiveImage : ComponentBase
{
	/// <summary>
	/// Gets or sets the responsive image helper.
	/// </summary>
	[Inject]
	protected IResponsiveImageHelper ResponsiveImageHelper { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the CSS class that is applied to the div that wraps the generated img tag.
	/// To apply a CSS class directly to the img tag, use the "class" attribute.
	/// </summary>
	[Parameter]
	public string? CssClass { get; set; }

	/// <summary>
	/// Gets or sets the maximum pixel density. Defaults to 3.
	/// </summary>
	[Parameter]
	public virtual int MaxPixelDensity { get; set; } = 3;

	/// <summary>
	/// Gets or sets the URL.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public virtual string Url { get; set; } = null!;

	/// <summary>
	/// Gets or sets a value indicating whether the loading="lazy" attribute will be rendered
	/// by the img tag.
	/// </summary>
	[Parameter]
	public bool LazyLoadingEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the additional attributes.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; } = null!;

	/// <summary>
	/// Gets or sets the source set value used to populate the srcset attribute.
	/// </summary>
	protected string? SrcSetValue { get; set; }

	/// <inheritdoc />
	protected override void OnParametersSet() => Guard.IsNotNullOrWhiteSpace(Url, nameof(Url));

	/// <inheritdoc />
	protected override void OnInitialized() => InitializeImage();

	/// <summary>
	/// Initializes the image.
	/// </summary>
	protected virtual void InitializeImage() => SrcSetValue = ResponsiveImageHelper.GetPixelDensitySrcSetValue(Url, MaxPixelDensity);

	/// <inheritdoc />
	protected override void OnAfterRender(bool firstRender)
	{
		if (!firstRender)
			InitializeImage();
	}
}