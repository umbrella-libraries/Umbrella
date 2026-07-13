using System.Net.Http;
using System.Net.Http.Json;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.Utilities.Http.Constants;
using Xunit;

namespace Umbrella.Testing.AspNetCore.Http;

/// <summary>
/// Assertion extensions for HTTP responses containing Umbrella problem details.
/// </summary>
public static class HttpResponseMessageAssertionExtensions
{
	private const string ProblemDetailsMediaType = "application/problem+json";

	/// <summary>
	/// Asserts that the response contains an <see cref="UmbrellaProblemDetails"/> body with the expected status code.
	/// </summary>
	/// <param name="response">The response to assert.</param>
	/// <param name="expectedStatusCode">The expected HTTP status code.</param>
	/// <param name="cancellationToken">The cancellation token used when reading the response body.</param>
	/// <returns>The deserialized problem details.</returns>
	public static async Task<UmbrellaProblemDetails> AssertUmbrellaProblemDetailsAsync(
		this HttpResponseMessage response,
		HttpStatusCode expectedStatusCode,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(response);

		Assert.Equal(expectedStatusCode, response.StatusCode);
		Assert.Equal(ProblemDetailsMediaType, response.Content.Headers.ContentType?.MediaType);

		UmbrellaProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<UmbrellaProblemDetails>(cancellationToken);

		Assert.NotNull(problemDetails);
		Assert.Equal((int)expectedStatusCode, problemDetails.Status);

		return problemDetails;
	}

	/// <summary>
	/// Asserts that the response contains an <see cref="UmbrellaValidationProblemDetails"/> body with the expected
	/// status code and at least one validation error.
	/// </summary>
	/// <param name="response">The response to assert.</param>
	/// <param name="expectedStatusCode">The expected HTTP status code.</param>
	/// <param name="cancellationToken">The cancellation token used when reading the response body.</param>
	/// <returns>The deserialized validation problem details.</returns>
	public static async Task<UmbrellaValidationProblemDetails> AssertUmbrellaValidationProblemDetailsAsync(
		this HttpResponseMessage response,
		HttpStatusCode expectedStatusCode,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(response);

		Assert.Equal(expectedStatusCode, response.StatusCode);
		Assert.Equal(ProblemDetailsMediaType, response.Content.Headers.ContentType?.MediaType);

		UmbrellaValidationProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<UmbrellaValidationProblemDetails>(cancellationToken);

		Assert.NotNull(problemDetails);
		Assert.Equal((int)expectedStatusCode, problemDetails.Status);
		Assert.NotEmpty(problemDetails.Errors);

		return problemDetails;
	}

	/// <summary>
	/// Asserts that the response is an Umbrella concurrency-stamp conflict.
	/// </summary>
	/// <param name="response">The response to assert.</param>
	/// <param name="cancellationToken">The cancellation token used when reading the response body.</param>
	/// <returns>The deserialized problem details.</returns>
	public static async Task<UmbrellaProblemDetails> AssertConcurrencyStampMismatchAsync(
		this HttpResponseMessage response,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(response);

		UmbrellaProblemDetails problemDetails = await response.AssertUmbrellaProblemDetailsAsync(HttpStatusCode.Conflict, cancellationToken);

		Assert.Equal(HttpProblemCodes.ConcurrencyStampMismatch, problemDetails.Code);

		return problemDetails;
	}
}
