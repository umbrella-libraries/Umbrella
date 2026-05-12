namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// A pre-configured variant of <see cref="UmbrellaDataverseFileStorageProviderOptions"/> for the standard
/// Dataverse <c>annotation</c> table. The table name, primary key column, content column, file name column, and
/// MIME type column are all set to their well-known values — only <see cref="UmbrellaDataverseFileStorageProviderOptions.DataverseClient"/>
/// and, optionally, <see cref="UmbrellaDataverseFileStorageProviderOptions.MetadataColumnMappings"/> need to be supplied.
/// </summary>
public class UmbrellaDataverseAnnotationFileStorageProviderOptions : UmbrellaDataverseFileStorageProviderOptions
{
	/// <summary>
	/// Initializes a new instance of <see cref="UmbrellaDataverseAnnotationFileStorageProviderOptions"/> with
	/// the standard <c>annotation</c> table column names pre-populated.
	/// </summary>
	public UmbrellaDataverseAnnotationFileStorageProviderOptions()
	{
		TableName = "annotation";
		IdColumnName = "annotationid";
		DataColumnName = "documentbody";
		FileNameColumnName = "filename";
		MimeTypeColumnName = "mimetype";
		FileSizeColumnName = "filesize";
	}
}
