using Umbrella.Utilities.Exceptions;

namespace Umbrella.Maui.Exceptions;

/// <summary>
/// Represents an exception in the Umbrella.Maui library.
/// </summary>
/// <seealso cref="UmbrellaException" />
public class UmbrellaMauiException : UmbrellaException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaMauiException"/> class.
	/// </summary>
	public UmbrellaMauiException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaMauiException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public UmbrellaMauiException(string message) : base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaMauiException"/> class.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
	public UmbrellaMauiException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
