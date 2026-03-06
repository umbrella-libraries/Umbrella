using Microsoft.AspNetCore.Components;

namespace Umbrella.AspNetCore.WebUtilities.Components.Abstractions;

/// <summary>
/// Defines a component with a model parameter.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public interface IModelRazorComponent<TModel> : IComponent
{
	/// <summary>
	/// Gets or sets the model.
	/// </summary>
	TModel? Model { get; set; }
}
