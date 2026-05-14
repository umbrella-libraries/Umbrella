namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Identifies the mapping operation exposed by a generated Mapperly catalog entry.
/// </summary>
public enum UmbrellaMapperlyCatalogOperationKind
{
	/// <summary>
	/// A source value maps to a newly created destination instance.
	/// </summary>
	NewInstance,

	/// <summary>
	/// A source collection maps to a newly created destination collection.
	/// </summary>
	NewCollection,

	/// <summary>
	/// A source value maps onto an existing destination instance.
	/// </summary>
	ExistingInstance
}
