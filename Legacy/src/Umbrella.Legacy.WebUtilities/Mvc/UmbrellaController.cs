
using System.Web.Mvc;
using Microsoft.Extensions.Logging;

namespace Umbrella.Legacy.WebUtilities.Mvc;

/// <summary>
/// Serves as the base class for MVC controllers and encapsulates MVC specific functionality.
/// </summary>
public abstract class UmbrellaController : Controller
{
	#region Protected Properties		
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaController"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	protected UmbrellaController(ILogger logger)
	{
		Logger = logger;
	}
	#endregion

	/// <summary>
	/// Renders the specified view to a string.
	/// </summary>
	/// <param name="viewName">Name of the view.</param>
	/// <param name="model">The model.</param>
	/// <returns>An HTML string of the rendered view.</returns>
	protected string RenderViewToString(string viewName, object model)
	{
		ViewData.Model = model;

		using var sw = new StringWriter();

		ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(ControllerContext, viewName);
		var viewContext = new ViewContext(ControllerContext, viewResult.View, ViewData, TempData, sw);

		viewResult.View.Render(viewContext, sw);
		viewResult.ViewEngine.ReleaseView(ControllerContext, viewResult.View);

		return sw.GetStringBuilder().ToString();
	}
}