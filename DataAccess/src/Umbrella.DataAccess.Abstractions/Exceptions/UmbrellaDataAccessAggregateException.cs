using System.Runtime.Serialization;

namespace Umbrella.DataAccess.Abstractions.Exceptions;

/// <summary>
/// Represents an exception thrown during data access that results in multiple exceptions.
/// </summary>
/// <seealso cref="UmbrellaDataAccessException" />
[Serializable]
public class UmbrellaDataAccessAggregateException : UmbrellaDataAccessException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataAccessAggregateException"/> class.
	/// </summary>
	public UmbrellaDataAccessAggregateException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataAccessAggregateException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	public UmbrellaDataAccessAggregateException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataAccessAggregateException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	public UmbrellaDataAccessAggregateException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataAccessAggregateException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerExceptions">The inner exceptions.</param>
	public UmbrellaDataAccessAggregateException(string message, params Exception[] innerExceptions)
		: base(message, new AggregateException(innerExceptions))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataAccessAggregateException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	protected UmbrellaDataAccessAggregateException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
