using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Extensions;
using Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders.Common;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders.DataExpression;

/// <summary>
/// Serves as the base class for all Data Expression model binders and their descriptor counterparts.
/// </summary>
/// <typeparam name="TDescriptor">The type of the descriptor.</typeparam>
/// <seealso cref="IModelBinder" />
public abstract class DataExpressionModelBinder<TDescriptor> : IModelBinder
	where TDescriptor : IDataExpressionDescriptor
{
	private static readonly ConcurrentDictionary<Type, (bool isEnumerable, Type? elementType)> _enumerableTypeDataCache = new();
	private static readonly ConcurrentDictionary<Type, Type> _genericListTypeCache = new();

	/// <summary>
	/// Gets or sets the optional descriptor transformer which is applied to deserialized descriptors.
	/// </summary>
	protected internal static DataExpressionTransformer<TDescriptor>? DescriptorTransformer { get; set; }

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the data expression factory.
	/// </summary>
	protected IDataExpressionFactory DataExpressionFactory { get; }

	/// <summary>
	/// Gets the type of the descriptor.
	/// </summary>
	protected abstract Type DescriptorType { get; }

	/// <summary>
	/// Gets the type of the enumerable descriptor.
	/// </summary>
	protected abstract Type EnumerableDescriptorType { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DataExpressionModelBinder{TDescriptor}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dataExpressionFactory">The data expression factory.</param>
	protected DataExpressionModelBinder(
		ILogger logger,
		IDataExpressionFactory dataExpressionFactory)
	{
		Logger = logger;
		DataExpressionFactory = dataExpressionFactory;
	}

	/// <inheritdoc />
	public virtual Task BindModelAsync(ModelBindingContext bindingContext)
	{
		Guard.IsNotNull(bindingContext);

		ValueProviderResult result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

		if (result == ValueProviderResult.None)
			return Task.CompletedTask;

		bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);

		Type underlyingOrModelType = bindingContext.ModelMetadata.UnderlyingOrModelType;

		try
		{
			string value = result.FirstValue ?? string.Empty;

			bool isEnumerable = underlyingOrModelType.IsAssignableToGenericType(typeof(IEnumerable<>));
			Type deserializationType = isEnumerable ? EnumerableDescriptorType : DescriptorType;

			if (isEnumerable && !value.StartsWith('[') && !value.EndsWith(']'))
				value = $"[{value}]";

			object? model = JsonSerializer.Deserialize(value, deserializationType, BinderHelper.SerializerOptions);

			if (model is TDescriptor descriptor)
			{
				if (descriptor.IsValid())
				{
					if (DescriptorTransformer is not null)
					{
						TDescriptor[]? transferArray = null;

						try
						{
							transferArray = ArrayPool<TDescriptor>.Shared.Rent(1);
							transferArray[0] = descriptor;

							DescriptorTransformer(bindingContext, transferArray, 1);
						}
						finally
						{
							if (transferArray is not null)
								ArrayPool<TDescriptor>.Shared.Return(transferArray);
						}
					}

					object? modelResult = TransformDescriptor(underlyingOrModelType, descriptor);

					if (modelResult is not null)
					{
						bindingContext.Result = ModelBindingResult.Success(modelResult);
					}
					else
					{
						// We don't have a result here which means the descriptor couldn't be mapped
						// to a property on the target type.
						// Treat as success and add the descriptor to a list.
						bindingContext.HttpContext.TrackUnmatchedDataExpressionDescriptor(descriptor);
						bindingContext.Result = ModelBindingResult.Success(null);
					}
				}
			}
			else if (model is IEnumerable<TDescriptor> descriptors)
			{
				descriptors = descriptors
					.Where(x => x?.IsValid() == true)
					.Distinct(DataExpressionDescriptorComparer<TDescriptor>.Default);

				DescriptorTransformer?.Invoke(bindingContext, descriptors, descriptors.Count());

				var (modelResult, unmatchedItems) = TransformDescriptors(underlyingOrModelType, descriptors);

				if (modelResult is not null)
				{
					bindingContext.Result = ModelBindingResult.Success(modelResult);
				}

				if (unmatchedItems is { Count: > 0 })
				{
					bindingContext.HttpContext.TrackUnmatchedDataExpressionDescriptors((IEnumerable<IDataExpressionDescriptor>)unmatchedItems);

					if (modelResult is null)
						bindingContext.Result = ModelBindingResult.Success(null);
				}
			}
			else
			{
				throw new UmbrellaWebException($"The deserialized model is not of type {nameof(TDescriptor)} or {nameof(IEnumerable<TDescriptor>)}.");
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bindingContext.ModelName, result.FirstValue }))
		{
			_ = bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"Cannot convert value to {underlyingOrModelType}.");
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Transforms the model.
	/// </summary>
	/// <param name="underlyingOrModelType">Type of the underlying or model.</param>
	/// <param name="descriptor">The descriptor.</param>
	/// <returns>The transformation output which serves as the result of the model binding process.</returns>
	protected virtual object? TransformDescriptor(Type underlyingOrModelType, TDescriptor descriptor) => DataExpressionFactory.Create(underlyingOrModelType, descriptor);

	/// <summary>
	/// Transforms the descriptors.
	/// </summary>
	/// <param name="underlyingOrModelType">Type of the underlying or model.</param>
	/// <param name="descriptors">The descriptors.</param>
	/// <returns>The transformation output which serves as the result of the model binding process.</returns>
	/// <exception cref="UmbrellaWebException">The type being dealt with is not an enumerable. This should not be possible based on earlier checks.</exception>
	[SuppressMessage("Performance", "CA1851:Possible multiple enumerations of 'IEnumerable' collection", Justification = "Unavoidable")]
	protected virtual (object? modelResult, IReadOnlyCollection<TDescriptor> unmatchedItems) TransformDescriptors(Type underlyingOrModelType, IEnumerable<TDescriptor> descriptors)
	{
		Guard.IsNotNull(underlyingOrModelType);
		Guard.IsNotNull(descriptors);

		var (isEnumerable, elementType) = _enumerableTypeDataCache.GetOrAdd(underlyingOrModelType, type => type.GetIEnumerableTypeData());

		if (!isEnumerable || elementType is null)
			throw new UmbrellaWebException("The type being dealt with is not an enumerable. This should not be possible based on earlier checks.");

		object[]? lstExpression = null;
		int count = 0;

		var lstUnmatched = new List<TDescriptor>();

		try
		{
			if (!descriptors.TryGetNonEnumeratedCount(out int descriptorCount))
				descriptorCount = descriptors.Count();

			lstExpression = ArrayPool<object>.Shared.Rent(descriptorCount);

			foreach (TDescriptor descriptor in descriptors)
			{
				object? instance = DataExpressionFactory.Create(elementType, descriptor);

				if (instance is not null)
					lstExpression[count++] = instance;
				else
					lstUnmatched.Add(descriptor);
			}
		}
		finally
		{
			if (lstExpression is not null)
				ArrayPool<object>.Shared.Return(lstExpression);
		}

		if (count is 0)
			return (null, lstUnmatched);

		if (underlyingOrModelType.IsArray)
		{
			var target = Array.CreateInstance(elementType, count);
			Array.Copy(lstExpression, target, count);

			return (target, lstUnmatched);
		}

		// Guaranteed to be dealing with another enumerable type here - assume list.
		Type genericListType = _genericListTypeCache.GetOrAdd(elementType, type => typeof(List<>).MakeGenericType(elementType));

		if (Activator.CreateInstance(genericListType) is IList targetList)
		{
			lstExpression.ForEach(x => targetList.Add(x));

			return (targetList, lstUnmatched);
		}

		return (null, lstUnmatched);
	}
}