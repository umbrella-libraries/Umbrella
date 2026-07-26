using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders.DataExpression;

/// <summary>
/// Serves as the base class for all Data Expression model binder providers.
/// </summary>
/// <typeparam name="TModelBinder">The type of the model binder.</typeparam>
/// <seealso cref="IModelBinderProvider" />
public abstract class DataExpressionModelBinderProvider<TModelBinder> : IModelBinderProvider
{
	/// <summary>
	/// Gets the type of the data expression.
	/// </summary>
	protected abstract Type DataExpressionType { get; }

	/// <inheritdoc />
	public IModelBinder? GetBinder(ModelBinderProviderContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		Type underlyingModelType = context.Metadata.UnderlyingOrModelType;

		if (underlyingModelType.IsGenericType)
		{
			Type genericTypeDefinition = underlyingModelType.GetGenericTypeDefinition();

			if (genericTypeDefinition == DataExpressionType)
				return new BinderTypeModelBinder(typeof(TModelBinder));
		}

		var (isEnumerable, elementType) = underlyingModelType.GetIEnumerableTypeData();

		if (isEnumerable && elementType?.IsGenericType == true && elementType.GetGenericTypeDefinition() == DataExpressionType)
			return new BinderTypeModelBinder(typeof(TModelBinder));

		return null;
	}
}
