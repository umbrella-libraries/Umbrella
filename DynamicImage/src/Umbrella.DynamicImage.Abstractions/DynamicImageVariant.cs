using System.Runtime.InteropServices;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Used to specify the details of an allowed Dynamic Image variant. One or more of these variants are used to restrict what Dynamic Images can be generated.
/// This is primarily a mechanism to prevent user tampering when parsing image URLs to ensure only image sizes the target application needs are generated.
/// Focal points are intentionally excluded because they are typically supplied at runtime and should not participate in whitelist validation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DynamicImageVariant
{
	#region Public Properties		
	/// <summary>
	/// Gets the width.
	/// </summary>
	public int Width { get; }

	/// <summary>
	/// Gets the height.
	/// </summary>
	public int Height { get; }

	/// <summary>
	/// Gets the resize mode.
	/// </summary>
	public DynamicResizeMode ResizeMode { get; }

	/// <summary>
	/// Gets the format.
	/// </summary>
	public DynamicImageFormat Format { get; }
	#endregion

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageVariant"/> struct.
	/// </summary>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="format">The format.</param>
	public DynamicImageVariant(
		int width,
		int height,
		DynamicResizeMode resizeMode,
		DynamicImageFormat format)
	{
		Width = width;
		Height = height;
		ResizeMode = resizeMode;
		Format = format;
	}
}
