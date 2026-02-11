using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Csv.Exceptions;

/// <summary>
/// An exception that is thrown when there is an error related to CSV operations.
/// </summary>
internal sealed class UmbrellaCsvException : UmbrellaException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaCsvException"/> class with a specified error message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public UmbrellaCsvException(string message) : base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaCsvException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception, or a <see langword="null"/> reference if no inner exception is specified.</param>
	public UmbrellaCsvException(string message, Exception innerException) : base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaCsvException"/> class with no error message or inner exception.
	/// </summary>
	public UmbrellaCsvException()
	{
	}
}