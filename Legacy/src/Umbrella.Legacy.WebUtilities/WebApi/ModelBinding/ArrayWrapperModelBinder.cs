using System.Collections;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using Newtonsoft.Json.Linq;

namespace Umbrella.Legacy.WebUtilities.WebApi.ModelBinding;

/// <summary>
/// A model binder used to map a JSON encoded array to an enumerable collection.
/// </summary>
/// <seealso cref="IModelBinder" />
public class ArrayWrapperModelBinder : IModelBinder
{
	private readonly ConcurrentDictionary<Type, Type?> _modelTypeMappingDictionary = new();

	/// <inheritdoc />
	public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext)
	{
		if (actionContext is null)
			throw new ArgumentNullException(nameof(actionContext));
		if (bindingContext is null)
			throw new ArgumentNullException(nameof(bindingContext));

		if (typeof(IEnumerable).IsAssignableFrom(bindingContext.ModelType))
		{
			string? rawValue = null;

			ValueProviderResult val = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

			if (val is null)
			{
				// If nothing could be found, check if we are dealing with a POST, PUT or PATCH request
				var httpMethod = actionContext.Request.Method;

				if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
				{
					Stream stream = actionContext.Request.GetOwinContext().Request.Body;

					stream.Position = 0;
					using var reader = new StreamReader(stream);

					rawValue = reader.ReadToEnd();
				}

				if (string.IsNullOrWhiteSpace(rawValue))
					return false;
			}
			else
			{
				rawValue = val.RawValue as string;

				if (rawValue is null)
				{
					bindingContext.ModelState.AddModelError(bindingContext.ModelName, $"Wrong value type for {bindingContext.ModelType.FullName}");

					return false;
				}
			}

			try
			{
				if (string.IsNullOrEmpty(rawValue))
					return false;

				var token = JToken.Parse(rawValue!);

				if (token.Type == JTokenType.Array)
				{
					// We have already been sent an array so convert it to the target type and return
					bindingContext.Model = token.ToObject(bindingContext.ModelType);
				}
				else if (token.Type == JTokenType.Object)
				{
					// We need to determine what the type contained within the IEnumerable is.
					// We could really be dealing with eith a generic collection or an array so need to test for each.
					Type? underlyingType = _modelTypeMappingDictionary.GetOrAdd(bindingContext.ModelType, type =>
					{
						if (type.IsGenericType)
						{
							return type.GetGenericArguments().FirstOrDefault();
						}
						else if (type.IsArray)
						{
							return type.GetElementType();
						}

						return null;
					});

					if (underlyingType is null)
					{
						bindingContext.ModelState.AddModelError(bindingContext.ModelName, $"The underlying type of the type {bindingContext.ModelType.FullName} cannot be determined.");
					}
					else
					{
						object? instance = token.ToObject(underlyingType);

						// Regardless of whether the model type is a generic type or an array, we can just wrap the value in an array.
						var array = Array.CreateInstance(underlyingType, 1);
						array.SetValue(instance, 0);

						bindingContext.Model = array;
					}
				}

				return bindingContext.Model is not null;
			}
			catch
			{
				// Prevent a hard exception from being thrown and fall through to add an error to the model state.
			}

			bindingContext.ModelState.AddModelError(bindingContext.ModelName, $"Cannot convert value to {bindingContext.ModelType.FullName}.");
		}

		return false;
	}
}
