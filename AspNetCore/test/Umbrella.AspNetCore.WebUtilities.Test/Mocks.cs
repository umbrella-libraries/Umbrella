using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Umbrella.AspNetCore.WebUtilities.Hosting;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Helpers;
using Umbrella.WebUtilities.Hosting.Options;

namespace Umbrella.AspNetCore.WebUtilities.Test;

public static class Mocks
{
	public static UmbrellaWebHostingEnvironment CreateUmbrellaWebHostingEnvironment()
	{
		var logger = new Mock<ILogger<UmbrellaWebHostingEnvironment>>();

		var hostingEnvironment = new Mock<IWebHostEnvironment>();
		_ = hostingEnvironment.Setup(x => x.ContentRootPath).Returns(PathHelper.PlatformNormalize(@"C:\MockedWebApp\src\"));
		_ = hostingEnvironment.Setup(x => x.WebRootPath).Returns(PathHelper.PlatformNormalize(@"C:\MockedWebApp\src\wwwroot\"));

		var httpContextAccessor = new Mock<IHttpContextAccessor>();

		var context = new DefaultHttpContext();
		context.Request.Host = new HostString("www.test.com");

		_ = httpContextAccessor.Setup(x => x.HttpContext).Returns(context);

		return new UmbrellaWebHostingEnvironment(logger.Object,
			hostingEnvironment.Object,
			httpContextAccessor.Object,
			new UmbrellaWebHostingEnvironmentOptions(),
			CoreUtilitiesMocks.CreateCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility());
	}

	public static IMemoryCache CreateMemoryCache() => new MemoryCache(Options.Create(new MemoryCacheOptions()));

	public static TagHelperContext CreateTagHelperContext(TagHelperAttributeList attributes) => new(
			attributes,
			items: new Dictionary<object, object>(),
			uniqueId: Guid.NewGuid().ToString("N"));

	public static TagHelperOutput CreateImageTagHelperOutput(TagHelperAttributeList attributes, string tagName)
	{
		attributes ??= [];

		return new TagHelperOutput(
			tagName,
			attributes,
			getChildContentAsync: (useCachedResult, encoder) =>
			{
				var tagHelperContent = new DefaultTagHelperContent();
				_ = tagHelperContent.SetContent(default);
				return Task.FromResult<TagHelperContent>(tagHelperContent);
			});
	}

	//private static ViewContext CreateViewContext(string? requestPathBase = null)
	//{
	//	var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());

	//	if (requestPathBase is not null)
	//		actionContext.HttpContext.Request.PathBase = new PathString(requestPathBase);

	//	var metadataProvider = new EmptyModelMetadataProvider();
	//	var viewData = new ViewDataDictionary(metadataProvider, new ModelStateDictionary());
	//	var viewContext = new ViewContext(
	//		actionContext,
	//		Mock.Of<IView>(),
	//		viewData,
	//		Mock.Of<ITempDataDictionary>(),
	//		TextWriter.Null,
	//		new HtmlHelperOptions());

	//	return viewContext;
	//}
}
