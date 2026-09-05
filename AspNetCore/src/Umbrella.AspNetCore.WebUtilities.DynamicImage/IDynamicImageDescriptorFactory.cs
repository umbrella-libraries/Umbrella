using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage;

/// <summary>Issues image descriptors from trusted server-side metadata. Never expose this as an unrestricted signing endpoint.</summary>
public interface IDynamicImageDescriptorFactory
{
	/// <summary>Creates a descriptor, preserving a missing image as null.</summary>
	/// <param name="image">The image resolved by a file handler.</param>
	/// <param name="focalPointX">The optional saved horizontal coordinate.</param>
	/// <param name="focalPointY">The optional saved vertical coordinate.</param>
	/// <returns>The descriptor, or null for a missing file.</returns>
	DynamicImageDescriptor? Create(UmbrellaVersionedUrl? image, double? focalPointX = null, double? focalPointY = null);
}
