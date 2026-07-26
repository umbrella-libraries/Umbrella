using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using Newtonsoft.Json;
using Umbrella.Utilities.Data.Sorting;

namespace Umbrella.Legacy.WebUtilities.WebApi.ModelBinding;

/// <summary>
/// A WebAPI model binder that binds an incoming JSON string to either an array or a single instance
/// of <see cref="SortExpressionDescriptor"/>.
/// </summary>
/// <seealso cref="IModelBinder" />
public class SortExpressionDescriptorModelBinder : IModelBinder
{
	private static readonly Type _descriptorType = typeof(SortExpressionDescriptor);
	private static readonly Type _enumerableDescriptorType = typeof(IEnumerable<>).MakeGenericType(typeof(SortExpressionDescriptor));

	/// <summary>
	/// Binds the model to a value by using the specified controller context and binding context.
	/// </summary>
	/// <param name="actionContext">The action context.</param>
	/// <param name="bindingContext">The binding context.</param>
	/// <returns>
	/// true if model binding is successful; otherwise, false.
	/// </returns>
	public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext)
	{
		if (bindingContext is null)
			throw new ArgumentNullException(nameof(bindingContext));

		if (bindingContext.ModelType == _descriptorType || _enumerableDescriptorType.IsAssignableFrom(bindingContext.ModelType))
		{
			ValueProviderResult val = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

			if (val is null)
			{
				return false;
			}

			if (val.RawValue is not string rawValue)
			{
				bindingContext.ModelState.AddModelError(bindingContext.ModelName, $"Wrong value type for {nameof(SortExpressionDescriptor)}");
				return false;
			}

			try
			{
				if (_enumerableDescriptorType.IsAssignableFrom(bindingContext.ModelType) && !rawValue.StartsWith("[", StringComparison.Ordinal) && !rawValue.EndsWith("]", StringComparison.Ordinal))
					rawValue = $"[{rawValue}]";

				bindingContext.Model = JsonConvert.DeserializeObject(rawValue, bindingContext.ModelType);

				return true;
			}
			catch
			{
				bindingContext.ModelState.AddModelError(bindingContext.ModelName, $"Cannot convert value to {nameof(SortExpressionDescriptor)}.");
			}
		}

		return false;
	}
}
