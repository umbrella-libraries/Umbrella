


namespace Umbrella.Utilities.DataAnnotations.Enumerations;

/// <summary>
/// The type of validation to perform on an object instance.
/// </summary>
public enum ValidationType
{
	/// <summary>
	/// Specifies that shallow validation should be performed,
	/// i.e. only the properties that are members of the object instance will be validated.
	/// </summary>
	Shallow = 0,

	/// <summary>
	/// Specifies that deep validation should be performed,
	/// i.e. both the properties that are members and the nested properties of all complex objects will be validated.
	/// </summary>
	Deep = 1,

	/// <summary>
	/// Specifies that validation should not be performed.
	/// </summary>
	None = 2
}