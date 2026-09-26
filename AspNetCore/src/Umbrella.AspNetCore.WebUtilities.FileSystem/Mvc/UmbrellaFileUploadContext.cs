namespace Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc;

/// <summary>Immutable request and destination information passed to upload extension points.</summary>
/// <typeparam name="TUploadType">The application's upload-purpose enumeration.</typeparam>
public sealed record UmbrellaFileUploadContext<TUploadType> where TUploadType : struct, Enum
{
	/// <summary>Gets the original client filename, used only as metadata.</summary>
	public required string FileName { get; init; }
	/// <summary>Gets the original filename extension, including its leading dot.</summary>
	public required string FileExtension { get; init; }
	/// <summary>Gets the untrusted, client-declared content type.</summary>
	public required string ContentType { get; init; }
	/// <summary>Gets the declared body length, or null when it is unknown.</summary>
	public long? ContentLength { get; init; }
	/// <summary>Gets the parsed upload purpose.</summary>
	public required TUploadType UploadType { get; init; }
	/// <summary>Gets the generated temporary filename.</summary>
	public required string TempFileName { get; init; }
	/// <summary>Gets the temporary storage path.</summary>
	public required string TempPath { get; init; }
	/// <summary>Gets the URL returned for a successful upload.</summary>
	public required string TempUrl { get; init; }
}
