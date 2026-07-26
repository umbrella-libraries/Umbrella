using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Middleware;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;
using Umbrella.WebUtilities.Http.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Middleware;

public class DynamicImageMiddlewareTest
{
	[Fact]
	public void AddAllowedVariants_EnablesValidationAndMergesUniqueVariants()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageVariant existingVariant = new(90, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg);
		DynamicImageVariant generatedVariant = new(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.AllowedVariants =
		[
			existingVariant
		];

		DynamicImageMiddlewareOptions returnedOptions = options.AddAllowedVariants(
		[
			existingVariant,
			generatedVariant
		]);

		Assert.Same(options, returnedOptions);
		Assert.True(options.EnableValidation);
		Assert.Equal(2, options.AllowedVariants.Count);
		Assert.Contains(existingVariant, options.AllowedVariants);
		Assert.Contains(generatedVariant, options.AllowedVariants);
	}

	[Fact]
	public async Task InvokeAsync_AllowsRequest_WhenVariantsAddedViaHelper()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync((IUmbrellaFileInfo?)null);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		_ = options.AddAllowedVariants(
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		]);
		DynamicImageMiddleware middleware = CreateMiddleware(options);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotFound_WhenFocalPointSpecifiedForNonCropFocalPointMode()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageMiddleware middleware = CreateMiddleware(CreateOptions(fileProvider.Object));
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg?fpx=0.5&fpy=0.5");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotFound_WhenWidthExceedsConfiguredLimit()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.MaxWidthRequest = 500;
		DynamicImageMiddleware middleware = CreateMiddleware(options);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/600/200/Crop/png/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotFound_WhenGlobalVariantValidationFails()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.EnableValidation = true;
		options.AllowedVariants =
		[
			new DynamicImageVariant(90, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		];
		DynamicImageMiddleware middleware = CreateMiddleware(options);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task InvokeAsync_AllowsRuntimeFocalPoints_WhenWhitelistVariantMatches()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync((IUmbrellaFileInfo?)null);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.EnableValidation = true;
		options.AllowedVariants =
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg)
		];
		DynamicImageMiddleware middleware = CreateMiddleware(options);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/CropFocalPoint/png/images/test.jpg?fpx=0.25&fpy=0.75");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task InvokeAsync_RedirectsToFingerprintedUrl_PreservingRawQueryString()
	{
		DateTimeOffset lastModified = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var file = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = file.SetupGet(x => x.LastModified).Returns(lastModified);
		_ = file.SetupGet(x => x.Length).Returns(123L);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		var headerValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		_ = headerValueUtility.Setup(x => x.CreateLastModifiedHeaderValue(lastModified)).Returns("Mon, 14 Jul 2026 12:00:00 GMT");
		_ = headerValueUtility.Setup(x => x.CreateETagHeaderValue(lastModified, 123L)).Returns("\"abc123\"");
		DynamicImageMiddleware middleware = CreateMiddleware(CreateOptions(fileProvider.Object), headerValueUtility.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/CropFocalPoint/png/images/test.jpg?fpx=.25&fpy=.75&filter=first&filter=&encoded=%2Fimages%2Fhello%20world&flag");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status301MovedPermanently, context.Response.StatusCode);
		Assert.Equal($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/CropFocalPoint/png/{DynamicImageConstants.VersionTokenPathSegmentPrefix}abc123/images/test.jpg?fpx=.25&fpy=.75&filter=first&filter=&encoded=%2Fimages%2Fhello%20world&flag", context.Response.Headers.Location);
	}

	[Fact]
	public async Task InvokeAsync_RedirectsToNonFingerprintedUrl_WithoutTrailingQueryString()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var file = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = file.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		DynamicImageMiddleware middleware = CreateMiddleware(CreateOptions(fileProvider.Object));
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/{DynamicImageConstants.VersionTokenPathSegmentPrefix}abc123/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status301MovedPermanently, context.Response.StatusCode);
		Assert.Equal($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg", context.Response.Headers.Location);
	}

	private static DynamicImageMiddleware CreateMiddleware(DynamicImageMiddlewareOptions options, IHttpHeaderValueUtility? headerValueUtility = null)
	{
		RequestDelegate next = _ => Task.CompletedTask;

		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		var defaultHeaderValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		IMimeTypeUtility mimeTypeUtility = CoreUtilitiesMocks.CreateMimeTypeUtility((".jpg", "image/jpeg"), (".png", "image/png"));

		return new DynamicImageMiddleware(
			next,
			CoreUtilitiesMocks.CreateLogger<DynamicImageMiddleware>(),
			new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()),
			resizer.Object,
			headerValueUtility ?? defaultHeaderValueUtility.Object,
			mimeTypeUtility,
			options);
	}

	private static DynamicImageMiddlewareOptions CreateOptions(IUmbrellaFileStorageProvider fileProvider)
	{
		DynamicImageMiddlewareOptions options = new()
		{
			Mappings =
			[
				new DynamicImageMiddlewareMapping
				{
					FileProviderMapping = new UmbrellaFileStorageProviderMapping(fileProvider, "/images")
				}
			]
		};

		options.Sanitize();
		options.Validate();

		return options;
	}

	private static DefaultHttpContext CreateHttpContext(string pathAndQuery)
	{
		DefaultHttpContext context = new();

		if (pathAndQuery.Contains('?', StringComparison.Ordinal))
		{
			string[] parts = pathAndQuery.Split('?', 2);
			context.Request.Path = parts[0];
			context.Request.QueryString = new QueryString("?" + parts[1]);
		}
		else
		{
			context.Request.Path = pathAndQuery;
		}

		context.Response.Body = new MemoryStream();

		return context;
	}
}
