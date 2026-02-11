
namespace Umbrella.AspNetCore.Blazor.Components.FileUpload;

/// <summary>
/// Represents the current status of the <see cref="UmbrellaFileUpload"/> component.
/// </summary>
public enum UmbrellaFileUploadStatus
{
	/// <summary>
	/// The initial status when no file has been uploaded or selected.
	/// </summary>
	None,

	/// <summary>
	/// The status when a file has been selected but has yet to be uploaded.
	/// </summary>
	Selected,

	/// <summary>
	/// The status when the file is in the process of being uploaded to the server.
	/// </summary>
	Uploading,

	/// <summary>
	/// The status when the file has been successfully upload to the server.
	/// </summary>
	Uploaded
}