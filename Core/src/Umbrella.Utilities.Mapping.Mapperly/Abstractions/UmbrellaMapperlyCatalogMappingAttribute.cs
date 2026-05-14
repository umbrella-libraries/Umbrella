namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Declares a single mapping exposed by a generated Mapperly catalog.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
{
	/// <summary>
	/// Initializes a new <see cref="UmbrellaMapperlyCatalogMappingAttribute"/> instance.
	/// </summary>
	/// <param name="sourceType">The mapping source type.</param>
	/// <param name="destinationType">The mapping destination type.</param>
	/// <param name="operationKind">The supported mapping operation.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="sourceType"/> or <paramref name="destinationType"/> is null.
	/// </exception>
	public UmbrellaMapperlyCatalogMappingAttribute(
		Type sourceType,
		Type destinationType,
		UmbrellaMapperlyCatalogOperationKind operationKind)
	{
		SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
		DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
		OperationKind = operationKind;
	}

	/// <summary>
	/// Gets the mapping source type.
	/// </summary>
	public Type SourceType { get; }

	/// <summary>
	/// Gets the mapping destination type.
	/// </summary>
	public Type DestinationType { get; }

	/// <summary>
	/// Gets the supported mapping operation.
	/// </summary>
	public UmbrellaMapperlyCatalogOperationKind OperationKind { get; }
}
