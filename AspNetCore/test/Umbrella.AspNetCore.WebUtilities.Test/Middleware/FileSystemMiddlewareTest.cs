using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.AspNetCore.WebUtilities.FileSystem.Middleware;
using Umbrella.FileSystem.Abstractions;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.FileSystem.Middleware.Options;
using Umbrella.WebUtilities.Http.Abstractions;
namespace Umbrella.AspNetCore.WebUtilities.Test.Middleware;
public class FileSystemMiddlewareTest
{
	private static readonly byte[] _bytes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
	private static readonly DateTimeOffset _modified = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	[Theory]
	[InlineData("bytes=2-4", 206, "bytes 2-4/10", 2, 3)]
	[InlineData("bytes=2-", 206, "bytes 2-9/10", 2, 8)]
	[InlineData("bytes=-3", 206, "bytes 7-9/10", 7, 3)]
	[InlineData("bytes=-30", 206, "bytes 0-9/10", 0, 10)]
	[InlineData("bytes=7-100", 206, "bytes 7-9/10", 7, 3)]
	[InlineData("bytes=0-9223372036854775807", 206, "bytes 0-9/10", 0, 10)]
	[InlineData("bytes=10-", 416, "bytes */10", 0, 0)]
	[InlineData("bytes=-0", 416, "bytes */10", 0, 0)]
	[InlineData("bytes=9-2", 200, "", 0, 10)]
	[InlineData("bytes=abc", 200, "", 0, 10)]
	[InlineData("items=2-4", 200, "", 0, 10)]
	[InlineData("bytes=0-1,5-6", 200, "", 0, 10)]
	[InlineData("", 200, "", 0, 10)]
	public async Task RangeResponses(string range, int status, string contentRange, int offset, int count)
	{
		var (middleware, context, file) = Create();
		context.Request.Headers.Range = range;
		await middleware.InvokeAsync(context);
		Assert.Equal(status, context.Response.StatusCode);
		Assert.Equal(contentRange, context.Response.Headers.ContentRange.ToString());
		Assert.Equal(count, context.Response.ContentLength);
		Assert.Equal(_bytes.Skip(offset).Take(count), ((MemoryStream)context.Response.Body).ToArray());
		if (status != 416)
		{
			Assert.Equal("bytes", context.Response.Headers.AcceptRanges.ToString());
			Assert.Equal("video/mp4", context.Response.ContentType);
		}

		if (status == 206)
		{
			file.As<IUmbrellaRangeReadableFileInfo>().Verify(x => x.ReadRangeAsStreamAsync(offset, count, null, It.IsAny<CancellationToken>()), Times.Once);
		}
	}

	[Fact]
	public async Task EmptyFileRangeIsUnsatisfiable()
	{
		var (middleware, context, file) = Create();
		_ = file.SetupGet(x => x.Length).Returns(0);
		context.Request.Headers.Range = "bytes=0-";
		await middleware.InvokeAsync(context);
		Assert.Equal(416, context.Response.StatusCode);
		Assert.Equal("bytes */0", context.Response.Headers.ContentRange.ToString());
		Assert.Equal(0, context.Response.ContentLength);
	}

	[Theory]
	[InlineData("HEAD", true)]
	[InlineData("GET", false)]
	public async Task HeadAndUnsupportedProviders(string method, bool ranged)
	{
		var (middleware, context, file) = Create(ranged);
		context.Request.Method = method;
		context.Request.Headers.Range = "bytes=2-4";
		await middleware.InvokeAsync(context);
		Assert.Equal(200, context.Response.StatusCode);
		Assert.Equal(10, context.Response.ContentLength);
		if (method == "HEAD")
		{
			Assert.Equal(0, context.Response.Body.Length);
			file.As<IUmbrellaRangeReadableFileInfo>().Verify(x => x.ReadRangeAsStreamAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
			file.Verify(x => x.WriteToStreamAsync(It.IsAny<Stream>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
		}
		else
		{
			Assert.False(context.Response.Headers.ContainsKey("Accept-Ranges"));
		}
	}

	[Theory]
	[InlineData("\"tag\"", true, 206)]
	[InlineData("W/\"tag\"", true, 200)]
	[InlineData("\"other\"", true, 200)]
	[InlineData("Thu, 01 Jan 2026 00:00:00 GMT", true, 206)]
	[InlineData("Fri, 02 Jan 2026 00:00:00 GMT", true, 200)]
	[InlineData("invalid", true, 200)]
	[InlineData("\"tag\"", false, 200)]
	public async Task IfRange(string condition, bool validators, int status)
	{
		var (middleware, context, file) = Create();
		if (!validators)
		{
			_ = file.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		}

		context.Request.Headers.Range = "bytes=2-4";
		context.Request.Headers.IfRange = condition;
		await middleware.InvokeAsync(context);
		Assert.Equal(status, context.Response.StatusCode);
	}

	[Theory]
	[InlineData("\"tag\"", 304)]
	[InlineData("\"other\"", 206)]
	[InlineData(null, 304)]
	public async Task ConditionalRequestsPrecedeRange(string? eTag, int status)
	{
		var (middleware, context, _) = Create();
		context.Request.Headers.Range = "bytes=2-4";
		if (eTag is not null)
		{
			context.Request.Headers.IfNoneMatch = eTag;
		}

		context.Request.Headers.IfModifiedSince = _modified.ToString("R");
		await middleware.InvokeAsync(context);
		Assert.Equal(status, context.Response.StatusCode);
		if (status == 304)
		{
			Assert.Equal(0, context.Response.Body.Length);
		}
	}

	[Fact]
	public async Task DeniedRangeDoesNotLeakPartialHeaders()
	{
		var (middleware, context, file) = Create();
		_ = file.As<IUmbrellaRangeReadableFileInfo>().Setup(x => x.ReadRangeAsStreamAsync(2, 3, null, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new UmbrellaFileAccessDeniedException("/videos/test.mp4"));
		context.Request.Headers.Range = "bytes=2-4";
		await middleware.InvokeAsync(context);
		Assert.Equal(404, context.Response.StatusCode);
		Assert.False(context.Response.Headers.ContainsKey("Content-Range"));
		Assert.False(context.Response.Headers.ContainsKey("Accept-Ranges"));
	}

	[Fact]
	public async Task TruncatedRangeClearsHeadersBeforeResponseStarts()
	{
		var (middleware, context, file) = Create();
		_ = file.As<IUmbrellaRangeReadableFileInfo>().Setup(x => x.ReadRangeAsStreamAsync(2, 3, null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new MemoryStream());
		context.Request.Headers.Range = "bytes=2-4";
		_ = await Assert.ThrowsAsync<UmbrellaWebException>(() => middleware.InvokeAsync(context));
		Assert.Equal(500, context.Response.StatusCode);
		Assert.False(context.Response.Headers.ContainsKey("Content-Range"));
		Assert.Null(context.Response.ContentLength);
	}

	[Fact]
	public async Task CancellationDuringOpenDoesNotBecomePartialContent()
	{
		var (middleware, context, file) = Create();
		using var cts = new CancellationTokenSource();
		context.RequestAborted = cts.Token;
		_ = file.As<IUmbrellaRangeReadableFileInfo>().Setup(x => x.ReadRangeAsStreamAsync(2, 3, null, cts.Token))
			.Returns(async () =>
			{
				await cts.CancelAsync();
				throw new OperationCanceledException(cts.Token);
			});
		context.Request.Headers.Range = "bytes=2-4";
		await middleware.InvokeAsync(context);
		Assert.False(context.Response.Headers.ContainsKey("Content-Range"));
	}

	[Fact]
	public async Task TruncationAfterResponseStartsAbortsConnectionAndDisposesSource()
	{
		var (middleware, context, file) = Create();
		using var body = new MemoryStream();
		var response = new Mock<IHttpResponseFeature>();
		_ = response.SetupAllProperties();
		response.Object.Headers = new HeaderDictionary();
		_ = response.SetupGet(x => x.HasStarted).Returns(() => body.Length > 0);
		context.Features.Set(response.Object);
		context.Response.Body = body;
		var lifetime = new Mock<IHttpRequestLifetimeFeature>();
		context.Features.Set(lifetime.Object);
		var source = new MemoryStream([2]);
		_ = file.As<IUmbrellaRangeReadableFileInfo>().Setup(x => x.ReadRangeAsStreamAsync(2, 3, null, It.IsAny<CancellationToken>()))
			.ReturnsAsync(source);
		context.Request.Headers.Range = "bytes=2-4";

		await middleware.InvokeAsync(context);

		lifetime.Verify(x => x.Abort(), Times.Once);
		Assert.Equal(206, context.Response.StatusCode);
		Assert.Equal(new byte[] { 2 }, body.ToArray());
		Assert.False(source.CanRead);
	}

	[Fact]
	public async Task MissingValidatorsStillAllowUnconditionalRange()
	{
		var (middleware, context, file) = Create();
		_ = file.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		context.Request.Headers.Range = "bytes=2-4";
		await middleware.InvokeAsync(context);
		Assert.Equal(206, context.Response.StatusCode);
		Assert.False(context.Response.Headers.ContainsKey("ETag"));
		Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
	}

	private static (FileSystemMiddleware Middleware, DefaultHttpContext Context, Mock<IUmbrellaFileInfo> File) Create(bool ranges = true)
	{
		var file = new Mock<IUmbrellaFileInfo>();
		if (ranges)
		{
			_ = file.As<IUmbrellaRangeReadableFileInfo>()
				.Setup(x => x.ReadRangeAsStreamAsync(It.IsAny<long>(), It.IsAny<long>(), null, It.IsAny<CancellationToken>()))
				.ReturnsAsync((long offset, long length, int? buffer, CancellationToken token) => new MemoryStream(_bytes.Skip((int)offset).Take((int)length).ToArray()));
		}

		_ = file.SetupGet(x => x.Length).Returns(_bytes.Length);
		_ = file.SetupGet(x => x.LastModified).Returns(_modified);
		_ = file.SetupGet(x => x.ContentType).Returns("video/mp4");
		_ = file.Setup(x => x.WriteToStreamAsync(It.IsAny<Stream>(), null, It.IsAny<CancellationToken>()))
			.Returns((Stream target, int? buffer, CancellationToken token) => target.WriteAsync(_bytes, token).AsTask());
		var provider = new Mock<IUmbrellaFileStorageProvider>();
		_ = provider.Setup(x => x.GetAsync("/videos/test.mp4", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		var options = new FileSystemMiddlewareOptions
		{
			FileSystemPathPrefix = "files",
			Mappings = [new() { FileProviderMapping = new(provider.Object, "/videos") }]
		};
		options.Sanitize();
		var headers = new Mock<IHttpHeaderValueUtility>();
		_ = headers.Setup(x => x.CreateETagHeaderValue(It.IsAny<DateTimeOffset>(), It.IsAny<long>())).Returns("\"tag\"");
		_ = headers.Setup(x => x.CreateLastModifiedHeaderValue(It.IsAny<DateTimeOffset>())).Returns(_modified.ToString("R"));
		var middleware = new FileSystemMiddleware(_ => Task.CompletedTask, NullLogger<FileSystemMiddleware>.Instance, headers.Object, options);
		var context = new DefaultHttpContext();
		context.Request.Method = "GET";
		context.Request.Path = "/files/videos/test.mp4";
		context.Response.Body = new MemoryStream();
		return (middleware, context, file);
	}
}
