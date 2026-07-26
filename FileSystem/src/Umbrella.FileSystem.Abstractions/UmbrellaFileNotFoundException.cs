using System.Runtime.Serialization;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// An exception thrown when there has been a problem accessing a file because it could not be found.
/// </summary>
/// <seealso cref="UmbrellaFileSystemException" />
[Serializable]
public sealed class UmbrellaFileNotFoundException : UmbrellaFileSystemException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileNotFoundException"/> class.
	/// </summary>
	public UmbrellaFileNotFoundException()
	{
	}

	/// <summary>
	/// Gets the subpath.
	/// </summary>
	public string? Subpath { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileNotFoundException"/> class.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	public UmbrellaFileNotFoundException(string subpath)
		: base($"The file located at {subpath} could not be found.")
	{
		Subpath = subpath;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileNotFoundException"/> class.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="innerException">The inner exception.</param>
	public UmbrellaFileNotFoundException(string subpath, Exception innerException)
		: base($"The file located at {subpath} could not be found.", innerException)
	{
		Subpath = subpath;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileNotFoundException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	private UmbrellaFileNotFoundException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
