namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A file handler for accessing files stored in the temporary files directory.
/// </summary>
/// <seealso cref="IUmbrellaFileHandler{NoGroupId}" />
public interface IUmbrellaTempFileHandler : IUmbrellaFileHandler<NoGroupId>
{
}