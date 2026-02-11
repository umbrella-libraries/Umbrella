


using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Umbrella.AspNetCore.WebUtilities.Extensions;

/// <summary>
/// Extention methods for the <see cref="Controller"/> class.
/// </summary>
public static class ControllerExtensions
{
	/// <summary>
	/// Renders the specified view to a string.
	/// </summary>
	/// <param name="controller">The controller.</param>
	/// <param name="model">The model.</param>
	/// <param name="viewNameOrPath">The view name or path.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task" /> whose result will contain the rendered string upon completion.</returns>
	public static async Task<string> RenderViewToStringAsync(this Controller controller, object? model = null, string? viewNameOrPath = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(controller);

		viewNameOrPath = string.IsNullOrWhiteSpace(viewNameOrPath) ? controller.ControllerContext.ActionDescriptor.ActionName : viewNameOrPath.Trim();

		controller.ViewData.Model = model;

		using var writer = new StringWriter();

		ICompositeViewEngine viewEngine = controller.HttpContext.RequestServices.GetRequiredService<ICompositeViewEngine>();

		ViewEngineResult viewResult = viewNameOrPath.EndsWith(".cshtml", StringComparison.Ordinal)
			? viewEngine.GetView(viewNameOrPath, viewNameOrPath, false)
			: viewEngine.FindView(controller.ControllerContext, viewNameOrPath, false);

		if (!viewResult.Success)
			return $"A view with the name '{viewNameOrPath}' could not be found";

		var viewContext = new ViewContext(
			controller.ControllerContext,
			viewResult.View,
			controller.ViewData,
			controller.TempData,
			writer,
			new HtmlHelperOptions()
		);

		await viewResult.View.RenderAsync(viewContext).ConfigureAwait(false);

		return writer.GetStringBuilder().ToString();
	}
}