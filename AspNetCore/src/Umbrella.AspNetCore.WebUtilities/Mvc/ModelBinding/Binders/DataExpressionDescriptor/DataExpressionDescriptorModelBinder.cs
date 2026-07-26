using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders.DataExpression;
using Umbrella.Utilities.Data.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders.DataExpressionDescriptor;

/// <summary>
/// Serves as the base class for all Data Expression Descriptor model binders.
/// </summary>
/// <typeparam name="TDescriptor">The type of the descriptor.</typeparam>
public abstract class DataExpressionDescriptorModelBinder<TDescriptor> : DataExpressionModelBinder<TDescriptor>
	where TDescriptor : IDataExpressionDescriptor
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DataExpressionDescriptorModelBinder{TDescriptor}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dataExpressionFactory">The data expression factory.</param>
	protected DataExpressionDescriptorModelBinder(
		ILogger logger,
		IDataExpressionFactory dataExpressionFactory)
		: base(logger, dataExpressionFactory)
	{
	}

	/// <inheritdoc />
	protected override object? TransformDescriptor(Type underlyingOrModelType, TDescriptor descriptor) => descriptor;

	/// <inheritdoc />
	protected override (object? modelResult, IReadOnlyCollection<TDescriptor> unmatchedItems) TransformDescriptors(Type underlyingOrModelType, IEnumerable<TDescriptor> descriptors) => descriptors switch
	{
		TDescriptor[] => (descriptors.Cast<TDescriptor>().ToArray(), Array.Empty<TDescriptor>()),
		_ => (descriptors.Cast<TDescriptor>().ToList(), Array.Empty<TDescriptor>())
	};
}