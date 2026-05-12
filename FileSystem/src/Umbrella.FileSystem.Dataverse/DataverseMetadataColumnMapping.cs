using CommunityToolkit.Diagnostics;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// Maps a logical metadata key name to a Dataverse column, including type information needed
/// for reading and writing the correct attribute type.
/// </summary>
public record DataverseMetadataColumnMapping
{
	/// <summary>
	/// The logical name of the Dataverse column (e.g. <c>"subject"</c>, <c>"ownerid"</c>).
	/// </summary>
	public string ColumnName { get; init; } = null!;

	/// <summary>
	/// The Dataverse attribute type for this column. Controls how values are converted when
	/// reading from or writing to Dataverse. Defaults to <see cref="DataverseMetadataColumnType.Text"/>.
	/// </summary>
	public DataverseMetadataColumnType ColumnType { get; init; } = DataverseMetadataColumnType.Text;

	/// <summary>
	/// For <see cref="DataverseMetadataColumnType.Lookup"/> and <see cref="DataverseMetadataColumnType.Owner"/>
	/// column types: the logical name of the related table (e.g. <c>"contact"</c>, <c>"systemuser"</c>).
	/// Required when <see cref="ColumnType"/> is <see cref="DataverseMetadataColumnType.Lookup"/> or
	/// <see cref="DataverseMetadataColumnType.Owner"/>.
	/// </summary>
	public string? LookupTableName { get; init; }

	/// <summary>
	/// Validates that required properties are set for the configured <see cref="ColumnType"/>.
	/// </summary>
	public void Validate()
	{
		Guard.IsNotNullOrWhiteSpace(ColumnName);

		if (ColumnType is DataverseMetadataColumnType.Lookup or DataverseMetadataColumnType.Owner)
			Guard.IsNotNullOrWhiteSpace(LookupTableName);
	}
}
