using System.Runtime.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Caching;

/// <summary>
/// Represents errors that occur during operations on an <see cref="IDistributedCache"/> instance during the execution of a method that is part
/// of the <see cref="IDistributedCacheExtensions"/> type.
/// </summary>
/// <seealso cref="UmbrellaException" />
[Serializable]
public class UmbrellaDistributedCacheException : UmbrellaException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDistributedCacheException"/> class.
	/// </summary>
	public UmbrellaDistributedCacheException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDistributedCacheException"/> class.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public UmbrellaDistributedCacheException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDistributedCacheException"/> class.
	/// </summary>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
	public UmbrellaDistributedCacheException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDistributedCacheException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	protected UmbrellaDistributedCacheException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}