using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Umbrella.AspNetCore.Shared.Components.Abstractions;
using Umbrella.WebUtilities.Bundling.Abstractions;
using Umbrella.WebUtilities.Security;

namespace Umbrella.AspNetCore.WebUtilities.Components.Bundling.Abstractions;

/// <summary>
/// A base class for components that render a bundle of style files.
/// </summary>
/// <typeparam name="TBundleUtility">The type of the bundle utility.</typeparam>
public abstract class BundleStyleComponentBase<TBundleUtility> : UmbrellaComponentBase
	where TBundleUtility : class, IBundleUtility
{
	private string? _contentOrPath;
	private bool _shouldRender = true;

	/// <summary>
	/// Gets the bundle utility.
	/// </summary>
	[Inject]
	protected TBundleUtility BundleUtility { get; private set; } = null!;

	/// <summary>
	/// Gets the nonce context.
	/// </summary>
	[Inject]
	protected NonceContext NonceContext { get; private set; } = null!;

	/// <summary>
	/// Gets or sets the name of the bundle.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public string Name { get; set; } = null!;

	/// <summary>
	/// Gets or sets a value indicating whether the contents of the bundle should be rendered inline in the HTML.
	/// </summary>
	[Parameter]
	public bool RenderInline { get; set; }

	/// <summary>
	/// Gets or sets the additional attributes to be added to the style tag.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public IDictionary<string, object> AdditionalAttributes { get; set; } = null!;

	/// <inheritdoc/>
	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		Guard.IsNotNullOrWhiteSpace(Name);
		Name = Name.TrimToLowerInvariant();
		Guard.IsNotNullOrWhiteSpace(Name);
	}

	/// <inheritdoc/>
	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		_contentOrPath = RenderInline ? await BundleUtility.GetStyleSheetContentAsync(Name) : await BundleUtility.GetStyleSheetPathAsync(Name);
	}

	/// <inheritdoc/>
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		Guard.IsNotNull(builder);

		base.BuildRenderTree(builder);

		if (string.IsNullOrEmpty(_contentOrPath))
			return;

		if (RenderInline)
		{
			builder.OpenElement(0, "style");
			builder.AddMultipleAttributes(1, AdditionalAttributes);
			builder.AddAttribute(2, "nonce", NonceContext.Current);
			builder.AddContent(3, _contentOrPath);
		}
		else
		{
			builder.OpenElement(0, "link");
			builder.AddMultipleAttributes(1, AdditionalAttributes);
			builder.AddAttribute(2, "rel", "stylesheet");
			builder.AddAttribute(3, "href", _contentOrPath);
		}

		builder.CloseElement();

		_shouldRender = false;
	}

	/// <inheritdoc/>
	protected override bool ShouldRender() => _shouldRender;
}