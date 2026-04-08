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
	Create = 1,

	/// <summary>
	/// The file can be accessed for reading, e.g. by downloading or streaming the file content.
	/// </summary>
	Read = 2,

	/// <summary>
	/// The file can be accessed for updating, e.g. by replacing the file content.
	/// </summary>
	Update = 3,

	/// <summary>
	/// The file can be accessed for deletion, e.g. by deleting the file from the file system.
	/// </summary>
	Delete = 4,

	/// <summary>
	/// The file can be accessed for moving or copying, e.g. by moving the file to a different location or copying the file
	/// to create a new file.
	/// </summary>
	MoveOrCopy = 5
}