using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>
/// A file storage provider backed by SharePoint via Microsoft Graph.
/// </summary>
/// <seealso cref="IUmbrellaFileStorageProvider" />
public interface IUmbrellaSharePointFileStorageProvider : IUmbrellaFileStorageProvider
{
}
