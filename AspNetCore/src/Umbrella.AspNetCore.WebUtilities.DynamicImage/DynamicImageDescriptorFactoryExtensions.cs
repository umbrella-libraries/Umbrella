using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage;

/// <summary>Combines file resolution and focal approval without changing file-handler contracts.</summary>
public static class DynamicImageDescriptorFactoryExtensions
{
	/// <summary>Resolves an image and approves its trusted saved coordinates.</summary>
	/// <typeparam name="TGroupId">The file group identifier type.</typeparam>
	/// <param name="factory">The descriptor factory.</param>
	/// <param name="fileHandler">The application's file handler.</param>
	/// <param name="groupId">The file group.</param>
	/// <param name="providerFileName">The stored file name.</param>
	/// <param name="focalPointX">The saved X coordinate.</param>
	/// <param name="focalPointY">The saved Y coordinate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The descriptor, or null for a missing file.</returns>
	public static async Task<DynamicImageDescriptor?> GetImageAsync<TGroupId>(this IDynamicImageDescriptorFactory factory, IUmbrellaFileHandler<TGroupId> fileHandler, TGroupId groupId, string providerFileName, double? focalPointX = null, double? focalPointY = null, CancellationToken cancellationToken = default)
		where TGroupId : IEquatable<TGroupId>
	{
		cancellationToken.ThrowIfCancellationRequested();
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(fileHandler);
		UmbrellaVersionedUrl? image = await fileHandler.GetVersionedWebFilePathAsync(groupId, providerFileName, cancellationToken).ConfigureAwait(false);
		return factory.Create(image, focalPointX, focalPointY);
	}
}
