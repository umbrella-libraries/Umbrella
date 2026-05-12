using CommunityToolkit.Diagnostics;
using Microsoft.PowerPlatform.Dataverse.Client;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// Options for the <see cref="UmbrellaDataverseFileStorageProvider"/>.
/// </summary>
public class UmbrellaDataverseFileStorageProviderOptions : UmbrellaFileStorageProviderOptionsBase, ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// The Dataverse client used to perform operations against the Dataverse environment.
	/// <see cref="ServiceClient"/> implements this interface and can be passed directly.
	/// Assign this in the options builder, typically by resolving a service from the DI container.
	/// </summary>
	public IOrganizationServiceAsync2 DataverseClient { get; set; } = null!;

	/// <summary>
	/// The logical name of the Dataverse table (e.g. <c>"note"</c>, <c>"cr123_attachment"</c>).
	/// </summary>
	public string TableName { get; set; } = null!;

	/// <summary>
	/// The logical name of the primary key column for <see cref="TableName"/>
	/// (e.g. <c>"noteid"</c> for the <c>note</c> table).
	/// </summary>
	public string IdColumnName { get; set; } = null!;

	/// <summary>
	/// The logical name of the Text Area (Memo) column that stores the file content as a base64 string.
	/// <para>
	/// Dataverse Memo columns support up to approximately 1,048,576 characters, which equates to roughly
	/// 786 KB of binary data after base64 encoding. Ensure the column is large enough for the expected file sizes.
	/// </para>
	/// </summary>
	public string DataColumnName { get; set; } = null!;

	/// <summary>
	/// The logical name of the column that stores the file name (including extension),
	/// e.g. <c>"filename"</c>.
	/// </summary>
	public string FileNameColumnName { get; set; } = null!;

	/// <summary>
	/// The optional logical name of the column that stores the MIME type of the file,
	/// e.g. <c>"mimetype"</c> on the standard Dataverse <c>annotation</c> table.
	/// When set, the MIME type derived from the file's extension is written to this column alongside
	/// the file content. When <see langword="null"/> (default), no MIME type column is updated.
	/// </summary>
	public string? MimeTypeColumnName { get; set; }

	/// <summary>
	/// The optional logical name of an integer column that stores the file size in bytes,
	/// e.g. <c>"filesize"</c> on the standard Dataverse <c>annotation</c> table.
	/// When set, this column is read during <c>GetAsync</c> and <c>EnumerateDirectoryAsync</c>
	/// so that <see cref="IUmbrellaFileInfo.Length"/> is populated without fetching the full file content.
	/// When <see langword="null"/> (default), <see cref="IUmbrellaFileInfo.Length"/> reports <c>-1</c> until content is read.
	/// </summary>
	public string? FileSizeColumnName { get; set; }

	/// <summary>
	/// When <see langword="true"/>, calling <see cref="IUmbrellaFileInfo.DeleteAsync"/> deletes the entire
	/// Dataverse record. When <see langword="false"/> (default), only the <see cref="DataColumnName"/> and
	/// <see cref="FileNameColumnName"/> columns are cleared, leaving the record intact.
	/// </summary>
	public bool DeleteRecordOnFileDelete { get; set; }

	/// <summary>
	/// Maps logical metadata key names to typed Dataverse column descriptors.
	/// <para>
	/// Example: <c>{ ["Title"] = new DataverseMetadataColumnMapping { ColumnName = "subject", ColumnType = DataverseMetadataColumnType.Text } }</c>
	/// </para>
	/// </summary>
	public Dictionary<string, DataverseMetadataColumnMapping> MetadataColumnMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

	/// <inheritdoc />
	public void Sanitize()
	{
		TableName = TableName?.Trim()!;
		IdColumnName = IdColumnName?.Trim()!;
		DataColumnName = DataColumnName?.Trim()!;
		FileNameColumnName = FileNameColumnName?.Trim()!;
		MimeTypeColumnName = MimeTypeColumnName?.Trim();
		FileSizeColumnName = FileSizeColumnName?.Trim();
	}

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNull(DataverseClient);
		Guard.IsNotNullOrWhiteSpace(TableName);
		Guard.IsNotNullOrWhiteSpace(IdColumnName);
		Guard.IsNotNullOrWhiteSpace(DataColumnName);
		Guard.IsNotNullOrWhiteSpace(FileNameColumnName);

		foreach (var (_, mapping) in MetadataColumnMappings)
			mapping.Validate();
	}
}
