using System.Runtime.Serialization;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// An exception class that represents errors that occur within the Dynamic Image infrastructure.
/// </summary>
/// <seealso cref="UmbrellaException" />
[Serializable]
public class UmbrellaDynamicImageException : UmbrellaException
{
	/// <summary>
	/// Gets the options.
	/// </summary>
	public DynamicImageOptions Options { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	public UmbrellaDynamicImageException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	public UmbrellaDynamicImageException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	public UmbrellaDynamicImageException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="options">The options.</param>
	public UmbrellaDynamicImageException(string message, DynamicImageOptions options = default)
		: base(message)
	{
		Options = options;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	/// <param name="options">The options.</param>
	public UmbrellaDynamicImageException(string message, Exception innerException, DynamicImageOptions options = default)
		: base(message, innerException)
	{
		Options = options;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="format">The format.</param>
	public UmbrellaDynamicImageException(string message, Exception innerException, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format)
		: base(message, innerException)
	{
		Options = new DynamicImageOptions("", width, height, resizeMode, format);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageException"/> class.
	/// </summary>
	/// <param name="info">The <see cref="SerializationInfo"></see> that holds the serialized object data about the exception being thrown.</param>
	/// <param name="context">The <see cref="StreamingContext"></see> that contains contextual information about the source or destination.</param>
	protected UmbrellaDynamicImageException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}
