using System.Globalization;

namespace Umbrella.DataAnnotations;

/// <summary>
/// Specifies a validation attribute that enforces a value to be within a defined range or equal to a specified floor
/// value.
/// </summary>
/// <remarks>
/// Use this attribute to validate that a property or parameter is either within the specified minimum and maximum
/// range, or exactly matches the designated floor value. This is useful in scenarios where a particular threshold value
/// is permitted in addition to a standard range. The attribute supports both <see cref="int"/> and <see cref="double"/>
/// value types.
/// </remarks>
public sealed class FloorOrRangeAttribute : RangeAttribute
{
	// TODO: When Union types are a thing, we can change the type of Floor to be a union of int and double.
	/// <summary>
	/// Gets the floor level associated with the current object.
	/// </summary>
	/// <remarks>
	/// The value represents the floor value and can be either an <see cref="int"/> or a <see cref="double"/> .
	/// </remarks>
	public object Floor { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="FloorOrRangeAttribute" /> class with the specified minimum, maximum,
	/// and floor values.
	/// </summary>
	/// <remarks>
	/// Use this constructor to define a range with an explicit floor value, ensuring that the <paramref name="floor"/>
	/// does not exceed the <paramref name="minimum"/> . This can be useful for scenarios where a lower bound distinct from
	/// the minimum is required for validation or display purposes.
	/// </remarks>
	/// <param name="minimum">
	/// The minimum allowable value for the range. Must be greater than or equal to the <paramref name="floor"/> .
	/// </param>
	/// <param name="maximum">The maximum allowable value for the range.</param>
	/// <param name="floor">
	/// The floor value for the range. Must be less than or equal to the <paramref name="minimum"/> .
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the <paramref name="floor"/> value is greater than the <paramref name="minimum"/> value.
	/// </exception>
	public FloorOrRangeAttribute(double minimum, double maximum, double floor)
		: base(minimum, maximum)
	{
		Floor = floor;

		if (floor > minimum)
			throw new ArgumentOutOfRangeException(nameof(floor), floor, $"The floor value must be less than or equal to the minimum value of {minimum}.");
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FloorOrRangeAttribute" /> class with the specified minimum, maximum,
	/// and floor values.
	/// </summary>
	/// <remarks>
	/// Use this constructor to define a range with an explicit floor value, ensuring that the <paramref name="floor"/>
	/// does not exceed the <paramref name="minimum"/> . This can be useful for scenarios where a lower bound distinct from
	/// the minimum is required for validation or display purposes.
	/// </remarks>
	/// <param name="minimum">
	/// The minimum allowable value for the range. Must be greater than or equal to the <paramref name="floor"/> .
	/// </param>
	/// <param name="maximum">The maximum allowable value for the range.</param>
	/// <param name="floor">
	/// The floor value for the range. Must be less than or equal to the <paramref name="minimum"/> .
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when the <paramref name="floor"/> value is greater than the <paramref name="minimum"/> value.
	/// </exception>
	public FloorOrRangeAttribute(int minimum, int maximum, int floor)
		: base(minimum, maximum)
	{
		Floor = floor;

		if (floor > minimum)
			throw new ArgumentOutOfRangeException(nameof(floor), floor, $"The floor value must be less than or equal to the minimum value of {minimum}.");
	}

	/// <inheritdoc />
	public override bool IsValid(object? value)
	{
		if (base.IsValid(value))
			return true;

		if (value is null)
			return false;

		if (Floor is int intFloor && Convert.ToInt32(value, CultureInfo.InvariantCulture) == intFloor)
			return true;

		if (Floor is double doubleFloor && Convert.ToDouble(value, CultureInfo.InvariantCulture) == doubleFloor)
			return true;

		return false;
	}

	/// <inheritdoc />
	public override string FormatErrorMessage(string name) => string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, Minimum, Maximum, Floor);
}