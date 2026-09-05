using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Imaging.Abstractions;

#pragma warning disable BL0005 // Component parameters are deliberately assigned in direct state-transition tests.

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Blazor;

public class UmbrellaFileImagePreviewUploadTest
{
	[Fact]
	public async Task InteractivePreviewRendersSelectorMarkerAndResponsiveCrop()
	{
		string html = await RenderAsync(CreateValidParameters());

		Assert.Contains("u-file-image-preview-upload__focal-selector", html, StringComparison.Ordinal);
		Assert.Contains("style=\"left: 25%; top: 75%\"", html, StringComparison.Ordinal);
		Assert.Contains("Clear focal point", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/ScaleDown/jpg/_v_version/images/test.webp 100w", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/200/100/ScaleDown/jpg/_v_version/images/test.webp 200w", html, StringComparison.Ordinal);
		Assert.Contains("<canvas", html, StringComparison.Ordinal);
		Assert.DoesNotContain("/CropFocalPoint/", html, StringComparison.Ordinal);
		Assert.DoesNotContain("fpx=", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task NonInteractivePreviewRendersFocalCropWithoutSelector()
	{
		Dictionary<string, object?> parameters = CreateValidParameters();
		parameters[nameof(UmbrellaFileImagePreviewUpload.EnableFocalPointSelection)] = false;

		string html = await RenderAsync(parameters);

		Assert.DoesNotContain("u-file-image-preview-upload__focal-selector", html, StringComparison.Ordinal);
		Assert.DoesNotContain("/ScaleDown/", html, StringComparison.Ordinal);
		Assert.Contains("CropFocalPoint", html, StringComparison.Ordinal);
		Assert.Contains("fpx=0.25&amp;fpy=0.75", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task RejectsIncompleteFocalPoint()
	{
		Dictionary<string, object?> parameters = CreateValidParameters();
		_ = parameters.Remove(nameof(UmbrellaFileImagePreviewUpload.FocalPointY));

		_ = await Assert.ThrowsAsync<ArgumentException>(() => RenderAsync(parameters));
	}

	[Fact]
	public async Task RejectsOutOfRangeFocalPoint()
	{
		Dictionary<string, object?> parameters = CreateValidParameters();
		parameters[nameof(UmbrellaFileImagePreviewUpload.FocalPointX)] = 1.01;

		_ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RenderAsync(parameters));
	}

	[Fact]
	public async Task RejectsInteractiveSelectionForNonFocalResizeMode()
	{
		Dictionary<string, object?> parameters = CreateValidParameters();
		parameters[nameof(UmbrellaFileImagePreviewUpload.ResizeMode)] = DynamicResizeMode.Crop;

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(parameters));
	}

	[Theory]
	[InlineData(150, 90, 100, 40, 200, 100, 0.25, 0.5)]
	[InlineData(-10, -20, 0, 0, 200, 100, 0, 0)]
	[InlineData(250, 150, 0, 0, 200, 100, 1, 1)]
	public void NormalizeFocalPointClampsToDisplayedImage(
		double clientX,
		double clientY,
		double imageLeft,
		double imageTop,
		double imageWidth,
		double imageHeight,
		double expectedX,
		double expectedY)
	{
		(double x, double y) = UmbrellaFileImagePreviewUpload.NormalizeFocalPoint(
			clientX,
			clientY,
			imageLeft,
			imageTop,
			imageWidth,
			imageHeight);

		Assert.Equal(expectedX, x);
		Assert.Equal(expectedY, y);
	}

	[Fact]
	public async Task UserChangesAndClearsFocalPointAtomically()
	{
		var component = new UmbrellaFileImagePreviewUpload { ResizeMode = DynamicResizeMode.CropFocalPoint };
		var changes = new List<UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs>();
		component.OnFocalPointChanged = EventCallback.Factory.Create<UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs>(
			new object(),
			changes.Add);

		await component.SetFocalPointAndNotifyAsync(0.2, 0.8);
		await component.ClearFocalPointAsync();

		Assert.Collection(
			changes,
			change =>
			{
				Assert.Equal(0.2, change.FocalPointX);
				Assert.Equal(0.8, change.FocalPointY);
			},
			change =>
			{
				Assert.Null(change.FocalPointX);
				Assert.Null(change.FocalPointY);
			});
	}

	[Fact]
	public async Task KeyboardFineAdjustmentUsesCurrentPointAndClampsEdges()
	{
		var component = new UmbrellaFileImagePreviewUpload { ResizeMode = DynamicResizeMode.CropFocalPoint };

		await component.SetFocalPointAndNotifyAsync(0.995, 0.5);
		await component.FocalPointKeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
		await component.FocalPointKeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp", ShiftKey = true });

		Assert.Equal(1, component.UpdatedFocalPointX);
		Assert.Equal(0.4, component.UpdatedFocalPointY!.Value, precision: 10);
	}

	[Fact]
	public void UpdateReplacesImageTokenAndFocalStateAtomically()
	{
		var component = new UmbrellaFileImagePreviewUpload { ResizeMode = DynamicResizeMode.CropFocalPoint };

		component.Update("/images/new.jpg", "token", 0.4, 0.6);

		Assert.Equal(0.4, component.UpdatedFocalPointX);
		Assert.Equal(0.6, component.UpdatedFocalPointY);

		component.Update("/images/replacement.jpg", "replacement-token");

		Assert.Null(component.UpdatedFocalPointX);
		Assert.Null(component.UpdatedFocalPointY);
	}

	[Fact]
	public void ExternalImageRemovalClearsAndLaterRestoresSuppliedFocalPoint()
	{
		var component = new TestFileImagePreviewUpload
		{
			Url = "/images/current.jpg",
			ResizeMode = DynamicResizeMode.CropFocalPoint,
			FocalPointX = 0.3,
			FocalPointY = 0.7
		};

		component.ApplyParameters();
		Assert.Equal(0.3, component.UpdatedFocalPointX);
		Assert.Equal(0.7, component.UpdatedFocalPointY);

		component.Url = null;
		component.ApplyParameters();
		Assert.Null(component.UpdatedFocalPointX);
		Assert.Null(component.UpdatedFocalPointY);

		component.Url = "/images/restored.jpg";
		component.ApplyParameters();
		Assert.Equal(0.3, component.UpdatedFocalPointX);
		Assert.Equal(0.7, component.UpdatedFocalPointY);
	}

	private static Dictionary<string, object?> CreateValidParameters()
		=> new()
		{
			[nameof(UmbrellaFileImagePreviewUpload.Url)] = "/images/test.jpg",
			[nameof(UmbrellaFileImagePreviewUpload.VersionToken)] = "version",
			[nameof(UmbrellaFileImagePreviewUpload.WidthRequest)] = 100,
			[nameof(UmbrellaFileImagePreviewUpload.HeightRequest)] = 50,
			[nameof(UmbrellaFileImagePreviewUpload.MaxPixelDensity)] = 1,
			[nameof(UmbrellaFileImagePreviewUpload.SizeWidths)] = "100,200",
			[nameof(UmbrellaFileImagePreviewUpload.ResizeMode)] = DynamicResizeMode.CropFocalPoint,
			[nameof(UmbrellaFileImagePreviewUpload.FocalPointX)] = 0.25,
			[nameof(UmbrellaFileImagePreviewUpload.FocalPointY)] = 0.75,
			[nameof(UmbrellaFileImagePreviewUpload.EnableFocalPointSelection)] = true
		};

	private static async Task<string> RenderAsync(IDictionary<string, object?> parameters)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new UmbrellaDynamicImageOptions());
		_ = services.AddSingleton<IResponsiveImageHelper>(CoreUtilitiesMocks.CreateResponsiveImageHelper());
		_ = services.AddSingleton<IDynamicImageUtility>(provider => new DynamicImageUtility(provider.GetRequiredService<ILogger<DynamicImageUtility>>()));
		_ = services.AddSingleton(Mock.Of<IUmbrellaDialogService>());
		_ = services.AddSingleton(Mock.Of<IUmbrellaBlazorInteropService>());

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			HtmlRootComponent output = await renderer.RenderComponentAsync<UmbrellaFileImagePreviewUpload>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}

	private sealed class TestFileImagePreviewUpload : UmbrellaFileImagePreviewUpload
	{
		public void ApplyParameters() => OnParametersSet();
	}
}

#pragma warning restore BL0005
