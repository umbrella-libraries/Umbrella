using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Http.Results;
using Microsoft.Extensions.Logging;
using Umbrella.Legacy.WebUtilities.WebApi.Filters;

namespace Umbrella.Legacy.WebUtilities.WebApi;

/// <summary>
/// Serves as the base class for API controllers and encapsulates API specific functionality.
/// </summary>
/// <seealso cref="ApiController" />
[ValidationActionFilter]
public abstract class UmbrellaApiController : ApiController
{
	#region Protected Properties		
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }
	#endregion

	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaApiController"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	protected UmbrellaApiController(ILogger logger)
	{
		Logger = logger;
	}
	#endregion

	#region Protected Methods
	/// <summary>
	/// Creates a 201 Created <see cref="StatusCodeResult"/>.
	/// </summary>
	/// <returns>A <see cref="StatusCodeResult"/> of 201.</returns>
	protected virtual StatusCodeResult Created() => new(HttpStatusCode.Created, this);

	/// <summary>
	/// Creates a 201 Created <see cref="CreatedNegotiatedContentResult{T}"/> with the specified content.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <returns>A <see cref="CreatedNegotiatedContentResult{T}"/> of 201.</returns>
	protected virtual CreatedNegotiatedContentResult<T> Created<T>(T content) => Created("", content);

	/// <summary>
	/// Creates a 404 NotFound <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 404.</returns>
	protected virtual ResponseMessageResult NotFound(string reason) => CreateStringContentResult(HttpStatusCode.NotFound, reason);

	/// <summary>
	/// Creates a 409 Conflict <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 409.</returns>
	protected virtual ResponseMessageResult Conflict(string reason) => CreateStringContentResult(HttpStatusCode.Conflict, reason);

	/// <summary>
	/// Creates a 500 InternalServerError <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 500.</returns>
	protected virtual ResponseMessageResult InternalServerError(string reason) => CreateStringContentResult(HttpStatusCode.InternalServerError, reason);

	/// <summary>
	/// Creates a 401 Unauthorized <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 401.</returns>
	protected virtual ResponseMessageResult Unauthorized(string reason) => CreateStringContentResult(HttpStatusCode.Unauthorized, reason);

	/// <summary>
	/// Creates a 403 Forbidden <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 403.</returns>
	protected virtual ResponseMessageResult Forbidden(string reason) => CreateStringContentResult(HttpStatusCode.Forbidden, reason);

	/// <summary>
	/// Creates a 405 MethodNotAllowed <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 405.</returns>
	protected virtual ResponseMessageResult MethodNotAllowed(string reason) => CreateStringContentResult(HttpStatusCode.MethodNotAllowed, reason);

	/// <summary>
	/// Creates a 204 NoContent <see cref="StatusCodeResult"/>.
	/// </summary>
	/// <returns>A <see cref="StatusCodeResult"/> of 204.</returns>
	protected virtual StatusCodeResult NoContent() => new(HttpStatusCode.NoContent, this);

	/// <summary>
	/// Creates a 429 TooManyRequests <see cref="ResponseMessageResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/> of 429.</returns>
	protected virtual ResponseMessageResult TooManyRequests(string reason) => CreateStringContentResult((HttpStatusCode)429, reason);

	/// <summary>
	/// Creates a <see cref="ResponseMessageResult"/> with the specified <paramref name="statusCode"/> and <paramref name="reason"/>.
	/// </summary>
	/// <param name="statusCode">The reason.</param>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ResponseMessageResult"/>.</returns>
#pragma warning disable CA2000 // Dispose objects before losing scope
	protected virtual ResponseMessageResult CreateStringContentResult(HttpStatusCode statusCode, string reason) => new(new HttpResponseMessage(statusCode)
	{
		Content = new StringContent(reason, Encoding.UTF8, "text/plain")
	});
#pragma warning restore CA2000 // Dispose objects before losing scope
	#endregion
}