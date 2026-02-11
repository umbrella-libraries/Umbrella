using System.Text.Json;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders.JsonArray;

/// <summary>
/// A model binder that can bind a JSON array to a model.
/// </summary>
public sealed class JsonArrayModelBinder : IModelBinder
{
	/// <inheritdoc />
	public Task BindModelAsync(ModelBindingContext bindingContext)
	{
		Guard.IsNotNull(bindingContext);

		var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

		if (valueProviderResult == ValueProviderResult.None)
			return Task.CompletedTask;

		string? jsonString = valueProviderResult.FirstValue;

		if (string.IsNullOrEmpty(jsonString))
			return Task.CompletedTask;

		try
		{
			object? result = JsonSerializer.Deserialize(jsonString, bindingContext.ModelType);
			bindingContext.Result = ModelBindingResult.Success(result);
		}
		catch (JsonException)
		{
			bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid JSON format");
		}

		return Task.CompletedTask;
	}
}