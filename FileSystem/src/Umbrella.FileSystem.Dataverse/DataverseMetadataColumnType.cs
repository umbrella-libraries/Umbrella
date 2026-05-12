namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// Specifies the Dataverse column type for a metadata mapping, controlling how values are
/// read from and written to the Dataverse record.
/// </summary>
public enum DataverseMetadataColumnType
{
	/// <summary>Text (String or Memo) column — values are read and written as <see langword="string"/>.</summary>
	Text,

	/// <summary>Two Options (Boolean) column — values are read and written as <see langword="bool"/>.</summary>
	Boolean,

	/// <summary>Whole Number column — values are read and written as <see langword="int"/>.</summary>
	Integer,

	/// <summary>Decimal Number column — values are read and written as <see langword="decimal"/>.</summary>
	Decimal,

	/// <summary>Date and Time column — values are read and written as <see cref="DateTime"/>.</summary>
	DateTime,

	/// <summary>
	/// Lookup column — values are read as the related record's <see cref="Guid"/> and written as an
	/// <see cref="Microsoft.Xrm.Sdk.EntityReference"/>. <see cref="DataverseMetadataColumnMapping.LookupTableName"/> is required.
	/// </summary>
	Lookup,

	/// <summary>
	/// Owner (SystemUser or Team) column — values are read as the owner record's <see cref="Guid"/> and written as an
	/// <see cref="Microsoft.Xrm.Sdk.EntityReference"/>. <see cref="DataverseMetadataColumnMapping.LookupTableName"/> is required
	/// (typically <c>"systemuser"</c> or <c>"team"</c>).
	/// </summary>
	Owner,
}
