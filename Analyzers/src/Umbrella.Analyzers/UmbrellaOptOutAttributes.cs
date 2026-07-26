namespace Umbrella.Analyzers;

/// <summary>
/// Indicates that a type should be excluded from the Umbrella model standards enforcement.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class UmbrellaExcludeFromModelStandardsAttribute : Attribute
{
	/// <summary>
	/// Gets the justification for excluding this type from model standards enforcement.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaExcludeFromModelStandardsAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for excluding this type from model standards enforcement.</param>
	public UmbrellaExcludeFromModelStandardsAttribute(string justification)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
	}
}

/// <summary>
/// Base attribute that indicates a property can skip the 'required' keyword requirement
/// in Umbrella model standards.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
[SuppressMessage("Performance", "CA1813:Avoid unsealed attributes", Justification = "Unsealed to allow it to be inherited.")]
public class UmbrellaAllowOptionalPropertyAttribute : Attribute
{
	/// <summary>
	/// Gets the justification for allowing this property to be optional.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAllowOptionalPropertyAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for allowing this property to be optional.</param>
	public UmbrellaAllowOptionalPropertyAttribute(string justification)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
	}
}

/// <summary>
/// Indicates that a property can be mutable (have a setter rather than init-only)
/// despite Umbrella model standards.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaAllowMutablePropertyAttribute : Attribute
{
	/// <summary>
	/// Gets the justification for allowing this property to be mutable.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAllowMutablePropertyAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for allowing this property to be mutable.</param>
	public UmbrellaAllowMutablePropertyAttribute(string justification)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
	}
}

/// <summary>
/// Indicates that a property is designed to be initialized after the instance is created.
/// This is semantically more specific than <see cref="UmbrellaAllowOptionalPropertyAttribute" />, expressing intent
/// that the property will be set later in the object's lifecycle.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaAllowLateInitializationAttribute : UmbrellaAllowOptionalPropertyAttribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAllowLateInitializationAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for allowing this property to be initialized after object creation.</param>
	public UmbrellaAllowLateInitializationAttribute(string justification) : base(justification)
	{
	}
}

/// <summary>
/// Indicates that a collection property can use a mutable collection type
/// instead of the read-only collection contract normally required by Umbrella model standards.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaAllowMutableCollectionAttribute : Attribute
{
	/// <summary>
	/// Gets the justification for allowing this collection property to be mutable.
	/// </summary>
	public string Justification { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAllowMutableCollectionAttribute"/> class.
	/// </summary>
	/// <param name="justification">The justification for allowing this collection property to be mutable.</param>
	public UmbrellaAllowMutableCollectionAttribute(string justification)
	{
		Justification = justification ?? throw new ArgumentNullException(nameof(justification));
	}
}
