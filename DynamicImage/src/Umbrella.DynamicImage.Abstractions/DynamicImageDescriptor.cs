namespace Umbrella.DynamicImage.Abstractions;

/// <summary>Image metadata resolved together on the server and safe to pass to a browser.</summary>
public sealed record DynamicImageDescriptor
{
	/// <summary>Gets the original image URL.</summary>
	public required string Url { get; init; }
	/// <summary>Gets the file version token.</summary>
	public required string? VersionToken { get; init; }
	/// <summary>Gets the optional approved focal point.</summary>
	public required DynamicImageFocalPoint? FocalPoint { get; init; }
	/// <summary>Gets the server-issued focal approval, not a file-access credential.</summary>
	public required string? FocalPointApproval { get; init; }
}
