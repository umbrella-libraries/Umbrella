using System.Runtime.Serialization;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A generic exception that represents an error that has occurred during operation of the Umbrella File System.
/// </summary>
/// <seealso cref="UmbrellaException" />
[Serializable]
public class UmbrellaFileSystemException : UmbrellaException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileSystemException"/> class.
	/// </summary>
	public UmbrellaFileSystemException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileSystemException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public UmbrellaFileSystemException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileSystemException"/> class.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
	public UmbrellaFileSystemException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileSystemException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	protected UmbrellaFileSystemException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
