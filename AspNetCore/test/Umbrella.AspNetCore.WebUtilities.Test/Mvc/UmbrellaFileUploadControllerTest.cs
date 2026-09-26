using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc;

public class UmbrellaFileUploadControllerTest
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Upload_ForwardsStreamAndWritesMetadata(bool customHeaders)
	{
		using var body = new MemoryStream([1, 2, 3]);
		var (controller, provider, file, handler) = Create(body);
		controller.CustomHeaders = customHeaders;
		controller.SetHeaders();
		controller.Request.Headers[customHeaders ? "Type" : "X-FileUploadType"] = "image";

		var result = Assert.IsType<CreatedResult>(await controller.PostAsync(TestContext.Current.CancellationToken));

		Assert.Equal(controller.SeenContext!.TempFileName, result.Value);
		Assert.Equal(controller.SeenContext.TempUrl, result.Location);
		Assert.Equal("client/type", controller.SeenContext.ContentType);
		Assert.Equal<string>(["validate", "prepare", "metadata", "result"], controller.Calls);
		provider.Verify(x => x.SaveAsync(controller.SeenContext.TempPath, body, null, It.IsAny<CancellationToken>()), Times.Once);
		file.Verify(x => x.SetMetadataValueAsync(UmbrellaFileSystemConstants.FileNameMetadataKey, "test.png", false, It.IsAny<CancellationToken>()), Times.Once);
		file.Verify(x => x.SetMetadataValueAsync("FileUploadType", "Image", false, It.IsAny<CancellationToken>()), Times.Once);
		handler.Verify(x => x.ApplyPermissionsAsync(file.Object, default, false, It.IsAny<CancellationToken>()), Times.Once);
		file.Verify(x => x.WriteMetadataChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
		provider.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		Assert.True(body.CanRead);
	}

	[Theory]
	[InlineData("X-FileName", null)]
	[InlineData("X-FileName", " ")]
	[InlineData("X-FileUploadType", null)]
	[InlineData("X-FileUploadType", "invalid")]
	[InlineData("X-FileUploadType", "99")]
	[InlineData("X-FileUploadType", "None")]
	public async Task InvalidHeaders_RejectBeforeStorage(string header, string? value)
	{
		using var body = new MemoryStream([1]);
		var (controller, provider, _, _) = Create(body);
		if (value is null)
			_ = controller.Request.Headers.Remove(header);
		else
			controller.Request.Headers[header] = value;
		Assert.Equal(400, Assert.IsType<ObjectResult>(await controller.PostAsync(TestContext.Current.CancellationToken)).StatusCode);
		provider.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EmptyBody_IsRejectedAndOnlySavedFilesAreCleaned(bool declaredEmpty)
	{
		using var body = new MemoryStream();
		var (controller, provider, file, _) = Create(body);
		_ = file.SetupGet(x => x.Length).Returns(0);
		controller.Request.ContentLength = declaredEmpty ? 0 : null;
		Assert.Equal(400, Assert.IsType<ObjectResult>(await controller.PostAsync(TestContext.Current.CancellationToken)).StatusCode);
		provider.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), declaredEmpty ? Times.Never : Times.Once);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Hooks_CanRejectWithoutStorage(bool duringPreparation)
	{
		using var body = new MemoryStream([1]);
		var (controller, provider, _, _) = Create(body);
		var rejection = new StatusCodeResult(415);
		if (duringPreparation)
			controller.Prepared = UmbrellaFileUploadStreamResult.Reject(rejection);
		else
			controller.ValidationResult = rejection;
		Assert.Same(rejection, await controller.PostAsync(TestContext.Current.CancellationToken));
		provider.Verify(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>()), Times.Never);
		provider.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Theory]
	[InlineData("none")]
	[InlineData("save")]
	[InlineData("metadata")]
	[InlineData("result")]
	[InlineData("cancel")]
	public async Task OwnedStreamsAreDisposed_AndFailedUploadsAreCleaned(string failure)
	{
		using var body = new MemoryStream([1]);
		using var replacement = new MemoryStream([2]);
		var (controller, provider, file, _) = Create(body);
		controller.Prepared = UmbrellaFileUploadStreamResult.Success(replacement, ownsStream: true);
		var error = new InvalidOperationException("Expected failure");
		if (failure == "save")
			_ = provider.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>())).ThrowsAsync(error);
		if (failure == "metadata")
			_ = file.Setup(x => x.WriteMetadataChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(error);
		if (failure == "result")
			controller.ResultException = error;
		if (failure == "cancel")
			_ = provider.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

		if (failure == "cancel")
			_ = await Assert.ThrowsAsync<OperationCanceledException>(() => controller.PostAsync(TestContext.Current.CancellationToken));
		else if (failure == "none")
			_ = Assert.IsType<CreatedResult>(await controller.PostAsync(TestContext.Current.CancellationToken));
		else
			Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.PostAsync(TestContext.Current.CancellationToken)).StatusCode);

		Assert.False(replacement.CanRead);
		Assert.True(body.CanRead);
		provider.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.Is<CancellationToken>(t => t.CanBeCanceled && !t.IsCancellationRequested)), failure == "none" ? Times.Never : Times.Once);
	}

	[Fact]
	public async Task CleanupFailure_DoesNotReplaceDevelopmentException()
	{
		using var body = new MemoryStream([1]);
		var (controller, provider, _, _) = Create(body, "Development");
		var original = new InvalidOperationException("Original");
		_ = provider.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>())).ThrowsAsync(original);
		_ = provider.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Cleanup"));
		Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => controller.PostAsync(TestContext.Current.CancellationToken)));
	}

	[Theory]
	[InlineData("Development", true)]
	[InlineData("Production", false)]
	public async Task Delay_IsDevelopmentOnlyAndCancellable(string environment, bool shouldCancel)
	{
		using var body = new MemoryStream([1]);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		var (controller, _, _, _) = Create(body, environment);
		controller.Delay = TimeSpan.FromMinutes(1);
		cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
		if (shouldCancel)
			_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.PostAsync(cancellation.Token));
		else
			_ = Assert.IsType<CreatedResult>(await controller.PostAsync(cancellation.Token));
	}

	[Fact]
	public async Task KnownExceptionMapping_IsHonoured()
	{
		using var body = new MemoryStream([1]);
		var (controller, provider, _, _) = Create(body);
		controller.ExceptionResult = new StatusCodeResult(429);
		_ = provider.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
		Assert.Same(controller.ExceptionResult, await controller.PostAsync(TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task RequestBody_IsNeverOwned()
	{
		using var body = new MemoryStream([1]);
		var (controller, _, _, _) = Create(body);
		controller.Prepared = UmbrellaFileUploadStreamResult.Success(body, ownsStream: true);
		_ = await controller.PostAsync(TestContext.Current.CancellationToken);
		Assert.True(body.CanRead);
	}

	[Fact]
	public void ResultFactories_RejectInvalidState_AndBaseHasNoAuthorization()
	{
		_ = Assert.Throws<ArgumentNullException>(() => UmbrellaFileUploadStreamResult.Success(null!, false));
		_ = Assert.Throws<ArgumentNullException>(() => UmbrellaFileUploadStreamResult.Reject(null!));
		using var closed = new MemoryStream();
		closed.Dispose();
		_ = Assert.Throws<ArgumentException>(() => UmbrellaFileUploadStreamResult.Success(closed, false));
		Assert.Empty(typeof(UmbrellaFileUploadController<Purpose>).GetCustomAttributes(typeof(AuthorizeAttribute), true));
	}

	private static (TestController Controller, Mock<IUmbrellaFileStorageProvider> Provider, Mock<IUmbrellaFileInfo> File, Mock<IUmbrellaTempFileHandler> Handler) Create(Stream body, string environment = "Production")
	{
		var provider = new Mock<IUmbrellaFileStorageProvider>();
		var file = new Mock<IUmbrellaFileInfo>();
		var handler = new Mock<IUmbrellaTempFileHandler>();
		var hosting = new Mock<IWebHostEnvironment>();
		_ = hosting.SetupGet(x => x.EnvironmentName).Returns(environment);
		_ = file.SetupGet(x => x.Length).Returns(3);
		_ = provider.Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		_ = handler.Setup(x => x.GetTempFilePath(It.IsAny<string>())).Returns((string name) => "/temp/" + name);
		_ = handler.Setup(x => x.GetTempWebFilePath(It.IsAny<string>())).Returns((string name) => "/files/temp/" + name);
		var controller = new TestController(hosting.Object, provider.Object, handler.Object)
		{
			ControllerContext = new() { HttpContext = new DefaultHttpContext() }
		};
		controller.Request.Body = body;
		controller.SetHeaders();
		return (controller, provider, file, handler);
	}

	public enum Purpose { None, Image }

	private sealed class TestController : UmbrellaFileUploadController<Purpose>
	{
		public TestController(IWebHostEnvironment hosting, IUmbrellaFileStorageProvider provider, IUmbrellaTempFileHandler handler)
			: base(NullLogger<TestController>.Instance, hosting, provider, handler) { }
		public bool CustomHeaders { get; set; }
		public TimeSpan Delay { get; set; }
		public List<string> Calls { get; } = [];
		public IActionResult? ValidationResult { get; set; }
		public IActionResult? ExceptionResult { get; set; }
		public Exception? ResultException { get; set; }
		public UmbrellaFileUploadStreamResult? Prepared { get; set; }
		public UmbrellaFileUploadContext<Purpose>? SeenContext { get; private set; }
		protected override string FileNameHeaderName => CustomHeaders ? "Name" : base.FileNameHeaderName;
		protected override string ContentTypeHeaderName => CustomHeaders ? "Content" : base.ContentTypeHeaderName;
		protected override string UploadTypeHeaderName => CustomHeaders ? "Type" : base.UploadTypeHeaderName;
		protected override TimeSpan DevelopmentUploadDelay => Delay;
		public void SetHeaders()
		{
			Request.Headers.Clear();
			Request.Headers[FileNameHeaderName] = "test.png";
			Request.Headers[UploadTypeHeaderName] = "Image";
			Request.Headers[ContentTypeHeaderName] = "client/type";
		}
		protected override bool IsUploadTypeAllowed(Purpose uploadType) => base.IsUploadTypeAllowed(uploadType) && uploadType != Purpose.None;
		protected override Task<IActionResult?> ValidateUploadAsync(UmbrellaFileUploadContext<Purpose> context, CancellationToken cancellationToken)
		{
			SeenContext = context;
			Calls.Add("validate");
			return Task.FromResult(ValidationResult);
		}
		protected override Task<UmbrellaFileUploadStreamResult> PrepareUploadStreamAsync(UmbrellaFileUploadContext<Purpose> context, CancellationToken cancellationToken)
		{
			Calls.Add("prepare");
			return Prepared is null ? base.PrepareUploadStreamAsync(context, cancellationToken) : Task.FromResult(Prepared);
		}
		protected override Task WriteUploadMetadataAsync(UmbrellaFileUploadContext<Purpose> context, IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken)
		{
			Calls.Add("metadata");
			return base.WriteUploadMetadataAsync(context, fileInfo, cancellationToken);
		}
		protected override IActionResult CreateUploadResult(UmbrellaFileUploadContext<Purpose> context, IUmbrellaFileInfo fileInfo)
		{
			Calls.Add("result");
			if (ResultException is not null)
				throw ResultException;
			return base.CreateUploadResult(context, fileInfo);
		}
		protected override bool TryCreateExceptionResult(Exception exception, out IActionResult result)
		{
			if (ExceptionResult is not null)
			{
				result = ExceptionResult;
				return true;
			}

			return base.TryCreateExceptionResult(exception, out result);
		}
	}
}
