using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Razor.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Razor.Options;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor;

/// <summary>
/// A utility used to render a view to a string.
/// </summary>
/// <seealso cref="IRazorViewToStringRenderer" />
public class RazorViewToStringRenderer : IRazorViewToStringRenderer
{
	private readonly ILogger<RazorViewToStringRenderer> _logger;
	private readonly IRazorViewEngine _viewEngine;
	private readonly ITempDataProvider _tempDataProvider;
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IServiceProvider _serviceProvider;
	private readonly RazorViewToStringRendererOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="RazorViewToStringRenderer"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="viewEngine">The view engine.</param>
	/// <param name="tempDataProvider">The temp data provider.</param>
	/// <param name="httpContextAccessor">The HTTP context accessor.</param>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="options">The options.</param>
	public RazorViewToStringRenderer(
		ILogger<RazorViewToStringRenderer> logger,
		IRazorViewEngine viewEngine,
		ITempDataProvider tempDataProvider,
		IHttpContextAccessor httpContextAccessor,
		IServiceProvider serviceProvider,
		RazorViewToStringRendererOptions options)
	{
		_logger = logger;
		_viewEngine = viewEngine;
		_tempDataProvider = tempDataProvider;
		_httpContextAccessor = httpContextAccessor;
		_serviceProvider = serviceProvider;
		_options = options;
	}

	/// <inheritdoc />
	public async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model, HttpContext? httpContext = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			httpContext ??= _httpContextAccessor.HttpContext ?? _options.CreateHttpContext(_serviceProvider);

			if (httpContext is null)
				throw new UmbrellaWebException("The current httpContext is not available.");

			var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
			var view = FindView(actionContext, viewName);

			using var output = new StringWriter();

			var viewContext = new ViewContext(
				actionContext,
				view,
				new ViewDataDictionary<TModel>(
					metadataProvider: new EmptyModelMetadataProvider(),
					modelState: new ModelStateDictionary())
				{
					Model = model
				},
				new TempDataDictionary(
					actionContext.HttpContext,
					_tempDataProvider),
				output,
				new HtmlHelperOptions());

			await view.RenderAsync(viewContext).ConfigureAwait(false);

			return output.ToString();
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { viewName, model }))
		{
			throw new UmbrellaWebException("There has been a problem rendering the view.", exc);
		}
	}

	private IView FindView(ActionContext actionContext, string viewName)
	{
		var getViewResult = _viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true);

		if (getViewResult.Success)
			return getViewResult.View;

		var findViewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: true);

		if (findViewResult.Success)
			return findViewResult.View;

		var searchedLocations = getViewResult.SearchedLocations.Concat(findViewResult.SearchedLocations);

		string? errorMessage = string.Join(
			Environment.NewLine,
			new[] { $"Unable to find view '{viewName}'. The following locations were searched:" }.Concat(searchedLocations));

		throw new InvalidOperationException(errorMessage);
	}
}