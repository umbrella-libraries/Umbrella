namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Defines the file access policy to be applied to a file.
/// </summary>
public enum UmbrellaFileOperationType
{
	/// <summary>
	/// No access policy specified. This is the default value and should not be used when applying a policy to a file.
	/// </summary>
	None = 0,

	/// <summary>
	/// The file can be accessed for creation, e.g. by uploading a new file.
	/// </summary>
	/// <remarks>
	/// When trying to move a file, the create permission is required on the destination file, and the delete permission is
	/// required on the source file. This is because moving a file typically involves deleting the source file and creating
	/// a new file at the destination path. Therefore, both permissions are necessary to successfully move a file.
	/// </remarks>
	Create = 1,

	/// <summary>
	/// The file can be accessed for reading, e.g. by downloading or streaming the file content.
	/// </summary>
	/// <remarks>
	/// When trying to move a file, the create permission is required on the destination file, and the delete permission is
	/// required on the source file. This is because moving a file typically involves deleting the source file and creating
	/// a new file at the destination path. Therefore, both permissions are necessary to successfully move a file.
	/// </remarks>
	Read = 2,

	/// <summary>
	/// The file can be accessed for updating, e.g. by replacing the file content.
	/// </summary>
	Update = 3,

	/// <summary>
	/// The file can be accessed for deletion, e.g. by deleting the file from the file system.
	/// </summary>
	/// <remarks>
	/// When trying to move a file, the delete permission is required on the source file, and the create permission is
	/// required on the destination file. This is because moving a file typically involves deleting the source file and
	/// creating a new file at the destination path. Therefore, both permissions are necessary to successfully move a file.
	/// </remarks>
	Delete = 4
}