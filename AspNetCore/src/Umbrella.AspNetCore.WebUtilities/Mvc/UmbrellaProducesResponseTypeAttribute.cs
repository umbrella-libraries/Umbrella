using Microsoft.AspNetCore.Mvc;

namespace Umbrella.AspNetCore.WebUtilities.Mvc;

/// <summary>
/// An attribute that specifies the response type for a controller action.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class UmbrellaProducesResponseTypeAttribute : ProducesResponseTypeAttribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaProducesResponseTypeAttribute"/> class.
	/// </summary>
	/// <param name="statusCode">The status code.</param>
	public UmbrellaProducesResponseTypeAttribute(int statusCode)
		: base(statusCode)
	{
		Type = GetProblemDetailsType(statusCode);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaProducesResponseTypeAttribute"/> class.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <param name="statusCode">The status code.</param>
	public UmbrellaProducesResponseTypeAttribute(Type type, int statusCode)
		: base(type, statusCode)
	{
		Type = GetProblemDetailsType(statusCode);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaProducesResponseTypeAttribute"/> class.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <param name="statusCode">The status code.</param>
	/// <param name="contentType">The content type.</param>
	/// <param name="additionalContentTypes">Additional content types.</param>
	public UmbrellaProducesResponseTypeAttribute(Type type, int statusCode, string contentType, params string[] additionalContentTypes)
		: base(type, statusCode, contentType, additionalContentTypes)
	{
		Type = GetProblemDetailsType(statusCode);
		ContentType = contentType;
		AdditionalContentTypes = additionalContentTypes;
	}

	private static Type GetProblemDetailsType(int statusCode)
		=> statusCode is 400 or 422 ? typeof(UmbrellaValidationProblemDetails) : typeof(UmbrellaProblemDetails);

	/// <summary>
	/// Gets the media type of the content associated with the current instance.
	/// </summary>
	public string? ContentType { get; }

	/// <summary>
	/// Gets additional content types associated with the current instance.
	/// </summary>
	public IReadOnlyCollection<string> AdditionalContentTypes { get; } = [];
}

/// <summary>
/// Specifies the type of the response and status code returned by an action, including support for generic response
/// types and multiple content types.
/// </summary>
/// <remarks>Use this attribute to indicate the expected response type and HTTP status code for an action method
/// in ASP.NET Core, especially when the response type is generic or when multiple content types are supported. This
/// helps tools like Swagger generate accurate API documentation and assists clients in understanding the possible
/// responses. The attribute can be applied multiple times to document different status codes or content types for a
/// single action.</remarks>
/// <typeparam name="T">The type of the response returned by the action.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class UmbrellaProducesResponseTypeAttribute<T> : ProducesResponseTypeAttribute<T>
{
	/// <summary>
	/// Initializes a new instance of the UmbrellaProducesResponseTypeAttribute class with the specified HTTP status code.
	/// </summary>
	/// <param name="statusCode">The HTTP status code that the action can return. Must be a valid HTTP status code.</param>
	public UmbrellaProducesResponseTypeAttribute(int statusCode)
		: base(statusCode)
	{
	}

	/// <summary>
	/// Initializes a new instance of the UmbrellaProducesResponseTypeAttribute class with the specified HTTP status code
	/// and supported content types.
	/// </summary>
	/// <param name="statusCode">The HTTP status code that the action returns.</param>
	/// <param name="contentType">The primary content type that the action can produce in the response. Cannot be null or empty.</param>
	/// <param name="additionalContentTypes">An array of additional content types that the action can produce in the response. May be empty.</param>
	public UmbrellaProducesResponseTypeAttribute(int statusCode, string contentType, params string[] additionalContentTypes)
		: base(statusCode, contentType, additionalContentTypes)
	{
		ContentType = contentType;
		AdditionalContentTypes = additionalContentTypes;
	}

	/// <summary>
	/// Gets the media type of the content associated with the current instance.
	/// </summary>
	public string? ContentType { get; }

	/// <summary>
	/// Gets the collection of additional MIME content types supported by the component.
	/// </summary>
	/// <remarks>Use this property to specify or inspect extra content types that the component can process, in
	/// addition to its default supported types. The collection is read-only and may be empty if no additional content
	/// types are configured.</remarks>
	public IReadOnlyCollection<string> AdditionalContentTypes { get; } = [];
}
