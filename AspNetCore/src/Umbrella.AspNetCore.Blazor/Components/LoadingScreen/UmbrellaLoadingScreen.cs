


using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Rendering;
using System.Diagnostics.CodeAnalysis;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Enumerations;
using Umbrella.AspNetCore.Blazor.Constants;

namespace Umbrella.AspNetCore.Blazor.Components.LoadingScreen;

/// <summary>
/// A component that is used to show and hide a loading screen in response to events fired by the <see cref="ILoadingScreenService"/> service.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IDisposable" />
public class UmbrellaLoadingScreen : ComponentBase, IDisposable
{
	private bool _visible;

	[Inject]
	private ILoadingScreenService LoadingScreenUtility { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <inheritdoc />
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		Guard.IsNotNull(builder);

		base.BuildRenderTree(builder);

		if (_visible)
			builder.AddMarkupContent(0, "<div class=\"loading-screen\"><div class=\"loading-screen-icon\"></div></div>");
	}

	/// <inheritdoc />
	protected override void OnInitialized() => LoadingScreenUtility.OnStateChanged += LoadingScreenUtility_OnStateChanged;

	private void LoadingScreenUtility_OnStateChanged(LoadingScreenState state)
	{
		_visible = state switch
		{
			LoadingScreenState.Visible => true,
			LoadingScreenState.Hidden => false,
			_ => false,
		};

		StateHasChanged();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		LoadingScreenUtility.OnStateChanged -= LoadingScreenUtility_OnStateChanged;
		GC.SuppressFinalize(this);
	}
}