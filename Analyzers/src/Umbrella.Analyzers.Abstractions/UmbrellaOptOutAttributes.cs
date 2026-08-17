namespace Umbrella.Analyzers;

/// <summary>
/// Indicates that a model is designed for input binding and therefore permits non-required,
/// settable properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public sealed class UmbrellaInputModelAttribute : Attribute
{
}

/// <summary>
/// Indicates that a model property can omit the <c>required</c> modifier.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaAllowNonRequiredPropertyAttribute : Attribute
{
	/// <summary>
	/// Gets the justification for allowing the property to omit the <c>required</c> modifier.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAllowNonRequiredPropertyAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for allowing the property to omit <c>required</c>.</param>
	public UmbrellaAllowNonRequiredPropertyAttribute(string justification)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
	}
}

/// <summary>
/// Indicates that a model property can expose mutation through a setter, a mutable collection contract, or both.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaAllowMutablePropertyAttribute : Attribute
{
	/// <summary>
	/// Gets the justification for allowing mutation through the property.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAllowMutablePropertyAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for allowing mutation through the property.</param>
	public UmbrellaAllowMutablePropertyAttribute(string justification)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
	}
}
