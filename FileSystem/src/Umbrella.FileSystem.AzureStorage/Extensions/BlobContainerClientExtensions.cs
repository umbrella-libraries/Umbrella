// Copyright (c) Zinofi Digital Ltd. All Rights Reserved.
// Licensed under the MIT License.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Constants;

namespace Umbrella.FileSystem.AzureStorage.Extensions;

/// <summary>
/// Extension methods for the <see cref="BlobContainerClient"/> type.
/// </summary>
public static class BlobContainerClientExtensions
{
	/// <summary>
	/// Gets the blobs by directory name.
	/// </summary>
	/// <param name="container">The container.</param>
	/// <param name="directoryName">Name of the directory.</param>
	/// <param name="topLevelOnly">
	/// Specifies whether to get the top level only or to get all blobs in nested folders.
	/// </param>
	/// <param name="directorySeparator">The directory separator.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A list of all blobs inside the directory.</returns>
	public static async Task<List<BlobClient>> GetBlobsByDirectoryAsync(this BlobContainerClient container, string directoryName, bool topLevelOnly = true, char directorySeparator = '/', CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(container);

		string? prefix = !string.IsNullOrWhiteSpace(directoryName) ? CleanDirectoryName(directoryName, directorySeparator) : null;

		var lstBlob = new List<BlobClient>();
		var lstItem = new List<BlobItem>();

		var options = new GetBlobsByHierarchyOptions
		{
			Delimiter = directorySeparator.ToString(),
			Prefix = prefix
		};

		await foreach (BlobHierarchyItem item in container.GetBlobsByHierarchyAsync(options, cancellationToken))
		{
			if (!item.IsBlob)
			{
				if (!topLevelOnly)
					lstBlob.AddRange(await container.GetBlobsByDirectoryAsync(item.Prefix, topLevelOnly, directorySeparator, cancellationToken).ConfigureAwait(false));

				continue;
			}

			lstItem.Add(item.Blob);
		}

		lstBlob.AddRange(lstItem.Select(x => container.GetBlobClient(x.Name)));

		return lstBlob;
	}

	private static string CleanDirectoryName(string directoryName, char directorySeparator)
	{
		ReadOnlySpan<char> directoryNameReadOnlySpan = directoryName.AsSpan().Trim(directorySeparator);

		int length = directoryNameReadOnlySpan.Length + 1;

		Span<char> directoryNameSpan = length <= StackAllocConstants.MaxCharSize ? stackalloc char[length] : new char[length];
		directoryNameReadOnlySpan.CopyTo(directoryNameSpan);
		directoryNameSpan[^1] = directorySeparator;

		return directoryNameSpan.ToString();
	}
}