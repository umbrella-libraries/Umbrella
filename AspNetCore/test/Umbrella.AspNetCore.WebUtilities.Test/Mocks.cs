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

	public static TagHelperContext CreateTagHelperContext(TagHelperAttributeList attributes) => CreateTagHelperContext(attributes, new Dictionary<object, object>());

	/// <summary>
	/// Creates a <see cref="TagHelperContext"/> using the specified items dictionary. Pass a copy of a parent context's items to mimic the
	/// way the Razor infrastructure propagates them to the scope of a child tag helper.
	/// </summary>
	public static TagHelperContext CreateTagHelperContext(TagHelperAttributeList attributes, IDictionary<object, object> items) => new(
			attributes,
			items,
			uniqueId: Guid.NewGuid().ToString("N"));

	public static TagHelperOutput CreateImageTagHelperOutput(TagHelperAttributeList attributes, string tagName)
		=> CreateImageTagHelperOutput(attributes, tagName, executeChildrenAsync: null);

	/// <summary>
	/// Creates a <see cref="TagHelperOutput"/> whose child content delegate runs <paramref name="executeChildrenAsync"/>, which is how the
	/// Razor infrastructure executes nested tag helpers when the parent calls <see cref="TagHelperOutput.GetChildContentAsync()"/>.
	/// </summary>
	public static TagHelperOutput CreateImageTagHelperOutput(TagHelperAttributeList attributes, string tagName, Func<Task>? executeChildrenAsync)
	{
		attributes ??= [];

		return new TagHelperOutput(
			tagName,
			attributes,
			getChildContentAsync: async (useCachedResult, encoder) =>
			{
				if (executeChildrenAsync is not null)
					await executeChildrenAsync();

				var tagHelperContent = new DefaultTagHelperContent();
				_ = tagHelperContent.SetContent(default);
				return tagHelperContent;
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
