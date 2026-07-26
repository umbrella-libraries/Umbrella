using System.Runtime.Serialization;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Legacy.Utilities.Configuration;

/// <summary>
/// Represents an exception thrown during a configuration error with an Umbrella config section.
/// </summary>
/// <seealso cref="UmbrellaException" />
[Serializable]
public class UmbrellaConfigurationException : UmbrellaException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaConfigurationException"/> class.
	/// </summary>
	public UmbrellaConfigurationException()
		: base("A generic Umbrella configuration section has occurred.")
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaConfigurationException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public UmbrellaConfigurationException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaConfigurationException"/> class.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public UmbrellaConfigurationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaConfigurationException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	protected UmbrellaConfigurationException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
