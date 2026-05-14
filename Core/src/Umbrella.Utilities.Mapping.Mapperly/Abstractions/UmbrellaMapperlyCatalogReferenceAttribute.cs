namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Declares that the current assembly validates and composes mappings from the specified generated Mapperly catalog.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
{
	/// <summary>
	/// Initializes a new <see cref="UmbrellaMapperlyCatalogReferenceAttribute"/> instance.
	/// </summary>
	/// <param name="catalogType">The generated catalog type that should be considered in scope.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="catalogType"/> is null.</exception>
	public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType)
	{
		CatalogType = catalogType ?? throw new ArgumentNullException(nameof(catalogType));
	}

	/// <summary>
	/// Gets the generated catalog type that is in scope for the current assembly.
	/// </summary>
	public Type CatalogType { get; }
}
