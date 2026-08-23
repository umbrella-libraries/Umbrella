using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Umbrella.AspNetCore.Blazor.Services;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Blazor;

public class UmbrellaBlazorInteropServiceTest
{
	[Fact]
	public async Task InitializeImageFocalPointSelectorAsyncUsesUmbrellaInteropBundle()
	{
		var jsRuntime = new RecordingJsRuntime();
		var service = new UmbrellaBlazorInteropService(
			Mock.Of<ILogger<UmbrellaBlazorInteropService>>(),
			jsRuntime);

		await service.InitializeImageFocalPointSelectorAsync(default, TestContext.Current.CancellationToken);

		Assert.Equal("UmbrellaBlazorInterop.initializeImageFocalPointSelector", jsRuntime.Identifier);
		Assert.Collection(jsRuntime.Arguments!, argument => Assert.IsType<ElementReference>(argument));
	}

	[Fact]
	public async Task GetImageBoundsAsyncUsesUmbrellaInteropBundle()
	{
		var expected = new UmbrellaImageBounds(10, 20, 300, 150);
		var jsRuntime = new Mock<IJSRuntime>();
		_ = jsRuntime
			.Setup(x => x.InvokeAsync<UmbrellaImageBounds>(
				"UmbrellaBlazorInterop.getImageBounds",
				It.IsAny<CancellationToken>(),
				It.Is<object?[]?>(args => args != null && args.Length == 1 && args[0] != null && args[0]!.GetType() == typeof(ElementReference))))
			.Returns(new ValueTask<UmbrellaImageBounds>(expected));
		var service = new UmbrellaBlazorInteropService(
			Mock.Of<ILogger<UmbrellaBlazorInteropService>>(),
			jsRuntime.Object);

		UmbrellaImageBounds actual = await service.GetImageBoundsAsync(default, TestContext.Current.CancellationToken);

		Assert.Equal(expected, actual);
		jsRuntime.VerifyAll();
	}

	private sealed class RecordingJsRuntime : IJSRuntime
	{
		public string? Identifier { get; private set; }
		public object?[]? Arguments { get; private set; }

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
			=> InvokeAsync<TValue>(identifier, CancellationToken.None, args);

		public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
		{
			Identifier = identifier;
			Arguments = args;
			return ValueTask.FromResult(default(TValue)!);
		}
	}
}
