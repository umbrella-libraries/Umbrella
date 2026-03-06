using Microsoft.AspNetCore.Components;

namespace Umbrella.AspNetCore.WebUtilities.Components.Abstractions;

/// <summary>
/// A utility used to render a component to a string.
/// </summary>
public interface IRazorComponentToStringRenderer
{
	/// <summary>
	/// Renders the specified component to a string.
	/// </summary>
	/// <typeparam name="TComponent">The type of the component.</typeparam>
	/// <param name="parameters">The optional component parameters.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The rendered component as a string.</returns>
	Task<string> RenderComponentToStringAsync<TComponent>(IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
		where TComponent : IComponent;

	/// <summary>
	/// Renders the specified model component to a string.
	/// </summary>
	/// <typeparam name="TComponent">The type of the component.</typeparam>
	/// <typeparam name="TModel">The type of the model.</typeparam>
	/// <param name="model">The model.</param>
	/// <param name="parameters">The optional additional component parameters.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The rendered component as a string.</returns>
	Task<string> RenderComponentToStringAsync<TComponent, TModel>(TModel model, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
		where TComponent : IModelRazorComponent<TModel>;
}
