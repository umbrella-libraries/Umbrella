using Microsoft.Owin;

namespace Umbrella.Legacy.WebUtilities.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IOwinResponse"/> type.
/// </summary>
public static class IOwinResponseExtensions
{
	/// <summary>
	/// Sends the specified status code.
	/// </summary>
	/// <param name="response">The response.</param>
	/// <param name="statusCode">The status code.</param>
	/// <param name="sendNullBody">if set to <see langword="true"/>, sets <see cref="IOwinResponse.Body" /> to <see cref="Stream.Null"/>.</param>
	public static void SendStatusCode(this IOwinResponse response, HttpStatusCode statusCode, bool sendNullBody = true)
	{
		if (response is null)
			throw new ArgumentNullException(nameof(response));

		response.StatusCode = (int)statusCode;

		if (sendNullBody)
		{
			response.Body.Close();
			response.Body = Stream.Null;
		}
	}
}
