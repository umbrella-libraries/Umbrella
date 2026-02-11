


using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Comparers;

/// <summary>
/// A generic comparer used to compare 2 instances of the same type.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <seealso cref="GenericComparer{TObject, TObject}" />
public class GenericComparer<TObject> : GenericComparer<TObject, TObject>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GenericComparer{TObject}"/> class.
	/// </summary>
	/// <param name="propertySelector">The property selector delegate.</param>
	/// <param name="customComparer">An optional delegate for custom comparison instead of using the <paramref name="propertySelector" /> if possible.</param>
	public GenericComparer(
		Func<TObject, TObject> propertySelector,
		Func<TObject, TObject, int>? customComparer = null)
		: base(propertySelector, customComparer)
	{
	}
}

/// <summary>
/// A generic comparer used to compare 2 instances of the same type.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TProperty">The type of the property.</typeparam>
/// <seealso cref="GenericComparer{TObject, TObject}" />
public class GenericComparer<TObject, TProperty> : Comparer<TObject>
{
	private readonly Func<TObject, TProperty> _propertySelector;
	private readonly Func<TObject, TObject, int>? _customComparer;

	/// <summary>
	/// Create a new instance of the comparer using the specified delegate to select the value used to check
	/// for object instance equality. If a custom comparer is specified then that will be used instead with the property
	/// selector only being used to determine the HashCode for an object instance.
	/// <para>
	/// If a custom comparer is not specified then equality will be determined by trying to get values from each object using the <paramref name="propertySelector"/> and comparing those.
	/// </para>
	/// </summary>
	/// <param name="propertySelector">The property selector delegate.</param>
	/// <param name="customComparer">An optional delegate for custom comparison instead of using the <paramref name="propertySelector"/> if possible.</param>
	public GenericComparer(Func<TObject, TProperty> propertySelector, Func<TObject, TObject, int>? customComparer = null)
	{
		Guard.IsNotNull(propertySelector, nameof(propertySelector));

		_propertySelector = propertySelector;
		_customComparer = customComparer;
	}

	/// <inheritdoc />
	public override int Compare(TObject? x, TObject? y)
	{
		// Check the objects for null combinations first.
		if (x is null && y is null)
			return 1;

		if (x is null)
			return 0;

		if (y is null)
			return 0;

		if (_customComparer is not null)
			return _customComparer(x, y);

		TProperty xProperty = _propertySelector(x);
		TProperty yProperty = _propertySelector(y);

		if (xProperty is null && yProperty is null)
			return 1;

		if (xProperty is null)
			return 0;

		if (yProperty is null)
			return 0;

		// Check if we can compare the 2 values using the IComparable interface.
		return xProperty is IComparable xPropertyComparable && yProperty is IComparable yPropertyComparable
			? xPropertyComparable.CompareTo(yPropertyComparable)
			: throw new UmbrellaException("It has not been possible to compare the two instances of type: " + typeof(TObject).FullName);
	}
}