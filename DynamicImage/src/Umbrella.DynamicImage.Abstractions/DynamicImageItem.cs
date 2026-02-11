
using CommunityToolkit.Diagnostics;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Represents a DynamicImage.
/// </summary>
public class DynamicImageItem
{
	private DateTimeOffset? _lastModified;

	/// <summary>
	/// Gets or sets the last modified date
	/// </summary>
	public DateTimeOffset? LastModified
	{
		get => UmbrellaFileInfo is not null ? UmbrellaFileInfo.LastModified : _lastModified;
		set => _lastModified = value;
	}

	/// <summary>
	/// Gets the length of the image.
	/// </summary>
	public long Length => UmbrellaFileInfo?.Length ?? Content.Length;

	/// <summary>
	/// Gets or sets the image options.
	/// </summary>
	public DynamicImageOptions ImageOptions { get; set; }

	/// <summary>
	/// Gets or sets the content.
	/// </summary>
	public ReadOnlyMemory<byte> Content { private get; set; }

	// TODO: Need to be able to set these and retain their values using metadata.
	///// <summary>
	///// Gets or sets the width.
	///// </summary>
	//public int Width { get; set; }

	///// <summary>
	///// Gets or sets the height.
	///// </summary>
	//public int Height { get; set; }

	/// <summary>
	/// Gets or sets the <see cref="IUmbrellaFileInfo"/> instance.
	/// </summary>
	public IUmbrellaFileInfo? UmbrellaFileInfo { get; set; }

	/// <summary>
	/// Gets the content of the image file.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The image file content.</returns>
	public async Task<ReadOnlyMemory<byte>> GetContentAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (Content.Length is 0 && UmbrellaFileInfo is not null)
			Content = await UmbrellaFileInfo.ReadAsByteArrayAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

		return Content;
	}

	/// <summary>
	/// Writes the content of the image file to the target stream.
	/// </summary>
	/// <param name="target">The target.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/>.</returns>
	/// <remarks>This is just a convenience wrapper around the <see cref="IUmbrellaFileInfo.WriteToStreamAsync(Stream, int?, CancellationToken)"/> method.</remarks>
	public async Task WriteContentToStreamAsync(Stream target, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(target);

		if (Content.Length is 0 && UmbrellaFileInfo is not null)
		{
			await UmbrellaFileInfo.WriteToStreamAsync(target, cancellationToken: cancellationToken).ConfigureAwait(false);

			return;
		}

		if (Content.Length > 0)
		{
#if NET6_0_OR_GREATER
			await target.WriteAsync(Content, cancellationToken).ConfigureAwait(false);
#else
			await target.WriteAsync(Content.ToArray(), 0, Content.Length, cancellationToken).ConfigureAwait(false);
#endif
		}
	}
}