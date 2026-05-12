using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// A file storage provider backed by a Microsoft Dataverse table column.
/// </summary>
/// <seealso cref="IUmbrellaFileStorageProvider" />
public interface IUmbrellaDataverseFileStorageProvider : IUmbrellaFileStorageProvider
{
}
