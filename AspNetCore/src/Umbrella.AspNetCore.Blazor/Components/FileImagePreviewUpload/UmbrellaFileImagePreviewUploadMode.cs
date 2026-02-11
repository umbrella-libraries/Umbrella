
namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload;

/// <summary>
/// Represents upload modes, i.e. state, for the <see cref="UmbrellaFileImagePreviewUpload"/> component.
/// </summary>
public enum UmbrellaFileImagePreviewUploadMode
{
	/// <summary>
	/// Specifies that the component has no current file selection and therefore that the file input should be shown to allow
	/// a user to select a file.
	/// </summary>
	Upload,

	/// <summary>
	/// Specifies that a file has been selected and uploaded to the server. A preview of the uploaded image should be shown.
	/// </summary>
	Current,

	/// <summary>
	/// Specifies that the a file that has been uploaded does not match the currently uploaded file (if one existed already) and that an
	/// information message will be displayed to the user indicating this.
	/// </summary>
	New
}