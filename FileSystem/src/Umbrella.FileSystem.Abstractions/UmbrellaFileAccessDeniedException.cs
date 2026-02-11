
using System.Runtime.Serialization;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// An exception thrown when there is a permissions based error that occurs when accessing a file.
/// </summary>
/// <seealso cref="UmbrellaFileSystemException" />
[Serializable]
public sealed class UmbrellaFileAccessDeniedException : UmbrellaFileSystemException
{
	/// <summary>
	/// Gets the subpath.
	/// </summary>
	public string? Subpath { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileAccessDeniedException"/> class.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	public UmbrellaFileAccessDeniedException(string subpath)
		: base($"Access to the file located at {subpath} has been denied.")
	{
		Subpath = subpath;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileAccessDeniedException"/> class.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="innerException">The inner exception.</param>
	public UmbrellaFileAccessDeniedException(string subpath, Exception innerException)
		: base($"Access to the file located at {subpath} has been denied.", innerException)
	{
		Subpath = subpath;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileAccessDeniedException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	private UmbrellaFileAccessDeniedException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}