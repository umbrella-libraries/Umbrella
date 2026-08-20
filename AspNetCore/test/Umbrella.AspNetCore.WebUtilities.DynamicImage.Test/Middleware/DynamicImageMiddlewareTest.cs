using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Moq;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Middleware;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

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
	public void AddAllowedVariantCatalogs_MergesCatalogsAndEnablesValidationOnce()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageVariant existingVariant = new(90, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg);
		DynamicImageVariant sharedVariant = new(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg);
		DynamicImageVariant serverVariant = new(300, 200, DynamicResizeMode.Crop, DynamicImageFormat.WebP);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.AllowedVariants = [existingVariant];

		DynamicImageMiddlewareOptions returnedOptions = options.AddAllowedVariantCatalogs(
		[
			[sharedVariant],
			[sharedVariant, serverVariant]
		]);

		Assert.Same(options, returnedOptions);
		Assert.True(options.EnableValidation);
		Assert.Equal(3, options.AllowedVariants.Count);
		Assert.Contains(existingVariant, options.AllowedVariants);
		Assert.Contains(sharedVariant, options.AllowedVariants);
		Assert.Contains(serverVariant, options.AllowedVariants);
	}

	[Fact]
	public void AddAllowedVariantCatalogs_WhenValidationDisabled_PreservesDisabledState()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);

		_ = options.AddAllowedVariantCatalogs(
			[
				[new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)]
			],
			enableValidation: false);

		Assert.False(options.EnableValidation);
		_ = Assert.Single(options.AllowedVariants);
	}

	[Fact]
	public void AddAllowedVariantCatalogs_WhenCatalogIsNull_Throws()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		IEnumerable<DynamicImageVariant>? nullCatalog = null;

		_ = Assert.Throws<ArgumentNullException>(() => options.AddAllowedVariantCatalogs([nullCatalog!]));
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

	[Theory]
	[InlineData("image/webp")]
	[InlineData("image/avif")]
	public async Task InvokeAsync_UsesUrlFormatRegardlessOfAcceptHeader(string acceptHeader)
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var sourceFile = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = sourceFile.SetupGet(x => x.IsNew).Returns(true);
		_ = sourceFile.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(sourceFile.Object);

		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = resizer.Setup(x => x.SupportsFormat(DynamicImageFormat.Jpeg)).Returns(true);
		DynamicImageOptions requestedOptions = new("/images/test.png", 100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg);
		DynamicImageItem cachedItem = new()
		{
			Content = new byte[] { 1, 2, 3 },
			ImageOptions = requestedOptions
		};

		_ = resizer
			.Setup(x => x.GetCachedItemAsync(
				sourceFile.Object,
				It.Is<DynamicImageOptions>(options => options == requestedOptions),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(cachedItem);

		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.EnableUrlFingerprinting = false;
		_ = options.AddAllowedVariants(
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		]);
		DynamicImageMiddleware middleware = CreateMiddleware(options, resizer: resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg");
		context.Request.Headers.Accept = acceptHeader;
		context.Response.Headers.Vary = "Origin";

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
		Assert.Equal("image/jpeg", context.Response.ContentType);
		Assert.Equal(3, context.Response.ContentLength);

		string[] varyValues = context.Response.Headers.Vary
			.ToString()
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		Assert.Contains("Origin", varyValues, StringComparer.OrdinalIgnoreCase);
		Assert.DoesNotContain(HeaderNames.Accept, varyValues, StringComparer.OrdinalIgnoreCase);
		fileProvider.Verify(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>()), Times.Once);
		resizer.VerifyAll();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotModified_WhenIfNoneMatchMatches()
	{
		DateTimeOffset lastModified = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
		string lastModifiedHeaderValue = lastModified.ToString("R");
		string eTagValue = $"\"{UmbrellaFileVersionTokenUtility.Create(lastModified, 123L)}\"";
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var sourceFile = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = sourceFile.SetupGet(x => x.LastModified).Returns(lastModified);
		_ = sourceFile.SetupGet(x => x.Length).Returns(123L);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(sourceFile.Object);

		var headerValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		_ = headerValueUtility.Setup(x => x.CreateLastModifiedHeaderValue(lastModified)).Returns(lastModifiedHeaderValue);

		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = resizer.Setup(x => x.SupportsFormat(DynamicImageFormat.Jpeg)).Returns(true);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.EnableUrlFingerprinting = false;
		DynamicImageMiddleware middleware = CreateMiddleware(options, headerValueUtility.Object, resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg");
		context.Request.Headers.Accept = "image/webp";
		context.Request.Headers.IfNoneMatch = eTagValue;
		context.Request.Headers.IfModifiedSince = lastModifiedHeaderValue;
		context.Response.Headers.Vary = "Origin";

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
		Assert.Equal("no-cache", context.Response.Headers.CacheControl);
		Assert.Equal(eTagValue, context.Response.Headers.ETag);
		Assert.Equal(lastModifiedHeaderValue, context.Response.Headers.LastModified);
		Assert.Equal(0, context.Response.Body.Length);

		string[] varyValues = context.Response.Headers.Vary
			.ToString()
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		Assert.Contains("Origin", varyValues, StringComparer.OrdinalIgnoreCase);
		Assert.DoesNotContain(HeaderNames.Accept, varyValues, StringComparer.OrdinalIgnoreCase);
		fileProvider.Verify(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>()), Times.Once);
		headerValueUtility.VerifyAll();
		resizer.Verify(x => x.SupportsFormat(DynamicImageFormat.Jpeg), Times.Once);
		resizer.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotModified_WhenIfModifiedSinceMatches()
	{
		DateTimeOffset lastModified = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
		string lastModifiedHeaderValue = lastModified.ToString("R");
		string eTagValue = $"\"{UmbrellaFileVersionTokenUtility.Create(lastModified, 123L)}\"";
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var sourceFile = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = sourceFile.SetupGet(x => x.LastModified).Returns(lastModified);
		_ = sourceFile.SetupGet(x => x.Length).Returns(123L);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(sourceFile.Object);

		var headerValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		_ = headerValueUtility.Setup(x => x.CreateLastModifiedHeaderValue(lastModified)).Returns(lastModifiedHeaderValue);

		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = resizer.Setup(x => x.SupportsFormat(DynamicImageFormat.Jpeg)).Returns(true);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.EnableUrlFingerprinting = false;
		DynamicImageMiddleware middleware = CreateMiddleware(options, headerValueUtility.Object, resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg");
		context.Request.Headers.Accept = "image/jpeg";
		context.Request.Headers.IfModifiedSince = lastModifiedHeaderValue;

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
		Assert.Equal("no-cache", context.Response.Headers.CacheControl);
		Assert.Equal(eTagValue, context.Response.Headers.ETag);
		Assert.Equal(lastModifiedHeaderValue, context.Response.Headers.LastModified);
		Assert.False(context.Response.Headers.ContainsKey(HeaderNames.Vary));
		Assert.Equal(0, context.Response.Body.Length);
		fileProvider.Verify(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>()), Times.Once);
		headerValueUtility.VerifyAll();
		resizer.Verify(x => x.SupportsFormat(DynamicImageFormat.Jpeg), Times.Once);
		resizer.VerifyNoOtherCalls();
	}

	[Theory]
	[InlineData("101/200/Crop")]
	[InlineData("100/201/Crop")]
	[InlineData("100/200/UseWidth")]
	public async Task InvokeAsync_ReturnsNotFoundForUnregisteredTransform(string transform)
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		_ = options.AddAllowedVariants(
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		]);
		DynamicImageMiddleware middleware = CreateMiddleware(options, resizer: resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/{transform}/png/images/test.jpg");
		context.Request.Headers.Accept = "image/webp";

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		resizer.VerifyAll();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotFound_WhenExplicitlyRequestedFormatIsNotRegistered()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		_ = options.AddAllowedVariants(
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg)
		]);
		DynamicImageMiddleware middleware = CreateMiddleware(options);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.webp");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task InvokeAsync_AllowsExplicitlyRequestedFormat_WhenThatFormatIsRegistered()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync((IUmbrellaFileInfo?)null);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		_ = options.AddAllowedVariants(
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.WebP)
		]);
		DynamicImageMiddleware middleware = CreateMiddleware(options);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.webp");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task InvokeAsync_ReturnsNotFoundWithoutAccessingSource_WhenRequestedFormatIsNotSupportedByResizer()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = resizer.Setup(x => x.SupportsFormat(DynamicImageFormat.Avif)).Returns(false);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		_ = options.AddAllowedVariants(
		[
			new DynamicImageVariant(100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Avif)
		]);
		DynamicImageMiddleware middleware = CreateMiddleware(options, resizer: resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.avif");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		fileProvider.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
		resizer.VerifyAll();
	}

	[Fact]
	public async Task InvokeAsync_DoesNotNegotiateFormat()
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
		context.Request.Headers.Accept = "image/webp";

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
		Assert.False(context.Response.Headers.ContainsKey(HeaderNames.Vary));
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
		string versionToken = UmbrellaFileVersionTokenUtility.Create(lastModified, 123L);
		DynamicImageMiddleware middleware = CreateMiddleware(CreateOptions(fileProvider.Object), headerValueUtility.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/CropFocalPoint/png/images/test.jpg?fpx=.25&fpy=.75&filter=first&filter=&encoded=%2Fimages%2Fhello%20world&flag");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status301MovedPermanently, context.Response.StatusCode);
		Assert.Equal($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/CropFocalPoint/png/{DynamicImageConstants.VersionTokenPathSegmentPrefix}{versionToken}/images/test.jpg?fpx=.25&fpy=.75&filter=first&filter=&encoded=%2Fimages%2Fhello%20world&flag", context.Response.Headers.Location);
		Assert.Equal("no-store", context.Response.Headers.CacheControl);
	}

	[Fact]
	public async Task InvokeAsync_RedirectsToNonFingerprintedUrl_WithoutTrailingQueryString()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var file = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = file.SetupGet(x => x.IsNew).Returns(true);
		_ = file.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		DynamicImageMiddleware middleware = CreateMiddleware(CreateOptions(fileProvider.Object));
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/{DynamicImageConstants.VersionTokenPathSegmentPrefix}abc123/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status301MovedPermanently, context.Response.StatusCode);
		Assert.Equal($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg", context.Response.Headers.Location);
		Assert.Equal("no-store", context.Response.Headers.CacheControl);
	}

	[Fact]
	public async Task InvokeAsync_UsesContentHashForFingerprintAndConditionalETag_WhenLastModifiedIsMissing()
	{
		byte[] bytes = [1, 2, 3, 4, 5];
		string versionToken = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
		string eTagValue = $"\"{versionToken}\"";
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var file = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = file.SetupGet(x => x.IsNew).Returns(false);
		_ = file.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		_ = file.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
		_ = file.Setup(x => x.ReadAsStreamAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(() => new MemoryStream(bytes));
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		DynamicImageMiddleware middleware = CreateMiddleware(CreateOptions(fileProvider.Object));
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/{DynamicImageConstants.VersionTokenPathSegmentPrefix}{versionToken}/images/test.jpg");
		context.Request.Headers.IfNoneMatch = eTagValue;

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
		Assert.Equal(eTagValue, context.Response.Headers.ETag);
		Assert.False(context.Response.Headers.ContainsKey(HeaderNames.LastModified));
		Assert.Equal("no-cache", context.Response.Headers.CacheControl);
	}

	[Theory]
	[InlineData(MiddlewareHttpCacheability.NoCache, null, "no-cache")]
	[InlineData(MiddlewareHttpCacheability.Private, 31536000, "private, max-age=31536000, must-revalidate")]
	[InlineData(MiddlewareHttpCacheability.Public, 31536000, "public, max-age=31536000, must-revalidate")]
	[InlineData(MiddlewareHttpCacheability.NoStore, null, "no-store")]
	public async Task InvokeAsync_AppliesConfiguredCachePolicyToCanonicalFingerprintedResponse(
		MiddlewareHttpCacheability cacheability,
		int? maxAgeSeconds,
		string expectedCacheControl)
	{
		DateTimeOffset lastModified = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
		const long length = 123L;
		string versionToken = UmbrellaFileVersionTokenUtility.Create(lastModified, length);
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var file = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = file.SetupGet(x => x.LastModified).Returns(lastModified);
		_ = file.SetupGet(x => x.Length).Returns(length);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);

		DynamicImageOptions imageOptions = new("/images/test.png", 100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg, versionToken: versionToken);
		DynamicImageItem cachedItem = new() { Content = new byte[] { 1, 2, 3 }, ImageOptions = imageOptions };
		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = resizer.Setup(x => x.SupportsFormat(DynamicImageFormat.Jpeg)).Returns(true);
		_ = resizer.Setup(x => x.GetCachedItemAsync(file.Object, imageOptions, It.IsAny<CancellationToken>())).ReturnsAsync(cachedItem);
		var headerValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		_ = headerValueUtility.Setup(x => x.CreateLastModifiedHeaderValue(lastModified)).Returns(lastModified.ToString("R"));
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.Mappings[0].Cacheability = cacheability;
		options.Mappings[0].MaxAgeSeconds = maxAgeSeconds;
		DynamicImageMiddleware middleware = CreateMiddleware(options, headerValueUtility.Object, resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/{DynamicImageConstants.VersionTokenPathSegmentPrefix}{versionToken}/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
		Assert.Equal(expectedCacheControl, context.Response.Headers.CacheControl);
		bool emitsValidators = cacheability is not MiddlewareHttpCacheability.NoStore;
		Assert.Equal(emitsValidators ? $"\"{versionToken}\"" : string.Empty, context.Response.Headers.ETag.ToString());
		Assert.Equal(emitsValidators ? lastModified.ToString("R") : string.Empty, context.Response.Headers.LastModified.ToString());
		Assert.Equal(maxAgeSeconds.HasValue, context.Response.Headers.ContainsKey(HeaderNames.Expires));
	}

	[Fact]
	public async Task InvokeAsync_UsesNoStore_WhenFingerprintingCannotProduceTokenForPublicMapping()
	{
		var fileProvider = new Mock<IUmbrellaFileStorageProvider>(MockBehavior.Strict);
		var file = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = file.SetupGet(x => x.LastModified).Returns((DateTimeOffset?)null);
		_ = file.SetupGet(x => x.IsNew).Returns(false);
		_ = file.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
		_ = fileProvider.Setup(x => x.GetAsync("/images/test.png", It.IsAny<CancellationToken>())).ReturnsAsync(file.Object);
		DynamicImageOptions imageOptions = new("/images/test.png", 100, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg);
		DynamicImageItem cachedItem = new() { Content = new byte[] { 1, 2, 3 }, ImageOptions = imageOptions };
		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = resizer.Setup(x => x.SupportsFormat(DynamicImageFormat.Jpeg)).Returns(true);
		_ = resizer.Setup(x => x.GetCachedItemAsync(file.Object, imageOptions, It.IsAny<CancellationToken>())).ReturnsAsync(cachedItem);
		DynamicImageMiddlewareOptions options = CreateOptions(fileProvider.Object);
		options.Mappings[0].Cacheability = MiddlewareHttpCacheability.Public;
		options.Mappings[0].MaxAgeSeconds = 31536000;
		DynamicImageMiddleware middleware = CreateMiddleware(options, resizer: resizer.Object);
		DefaultHttpContext context = CreateHttpContext($"/{DynamicImageConstants.DefaultPathPrefix}/100/200/Crop/png/images/test.jpg");

		await middleware.InvokeAsync(context);

		Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
		Assert.Equal("no-store", context.Response.Headers.CacheControl);
		Assert.False(context.Response.Headers.ContainsKey(HeaderNames.Expires));
	}

	private static DynamicImageMiddleware CreateMiddleware(
		DynamicImageMiddlewareOptions options,
		IHttpHeaderValueUtility? headerValueUtility = null,
		IDynamicImageResizer? resizer = null)
	{
		RequestDelegate next = _ => Task.CompletedTask;

		var defaultResizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		_ = defaultResizer.Setup(x => x.SupportsFormat(It.IsAny<DynamicImageFormat>())).Returns(true);
		var defaultHeaderValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		IMimeTypeUtility mimeTypeUtility = CoreUtilitiesMocks.CreateMimeTypeUtility(
			("jpg", "image/jpeg"),
			("png", "image/png"),
			("webp", "image/webp"),
			("avif", "image/avif"));

		return new DynamicImageMiddleware(
			next,
			CoreUtilitiesMocks.CreateLogger<DynamicImageMiddleware>(),
			new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()),
			resizer ?? defaultResizer.Object,
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
