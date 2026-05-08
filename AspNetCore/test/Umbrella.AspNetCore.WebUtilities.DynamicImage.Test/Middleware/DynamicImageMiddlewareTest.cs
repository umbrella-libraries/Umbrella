using Microsoft.AspNetCore.Http;
using Moq;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Middleware;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;
using Umbrella.WebUtilities.Http.Abstractions;
using Xunit;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Middleware;

public class DynamicImageMiddlewareTest
{
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

	private static DynamicImageMiddleware CreateMiddleware(DynamicImageMiddlewareOptions options)
	{
		RequestDelegate next = _ => Task.CompletedTask;

		var resizer = new Mock<IDynamicImageResizer>(MockBehavior.Strict);
		var headerValueUtility = new Mock<IHttpHeaderValueUtility>(MockBehavior.Strict);
		IMimeTypeUtility mimeTypeUtility = CoreUtilitiesMocks.CreateMimeTypeUtility((".jpg", "image/jpeg"), (".png", "image/png"));

		return new DynamicImageMiddleware(
			next,
			CoreUtilitiesMocks.CreateLogger<DynamicImageMiddleware>(),
			new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()),
			resizer.Object,
			headerValueUtility.Object,
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
