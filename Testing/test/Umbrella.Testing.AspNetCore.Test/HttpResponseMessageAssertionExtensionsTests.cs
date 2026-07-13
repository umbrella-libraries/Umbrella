using System.Net.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.Testing.AspNetCore.Http;
using Umbrella.Utilities.Http.Constants;

namespace Umbrella.Testing.AspNetCore.Test;

public sealed class HttpResponseMessageAssertionExtensionsTests
{
	[Fact]
	public async Task AssertUmbrellaProblemDetailsAsyncReturnsDeserializedBody()
	{
		var expected = new UmbrellaProblemDetails
		{
			Status = StatusCodes.Status404NotFound,
			Title = "Not Found",
			Code = "Missing"
		};

		using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
		{
			Content = CreateProblemDetailsContent(expected)
		};

		UmbrellaProblemDetails actual = await response.AssertUmbrellaProblemDetailsAsync(HttpStatusCode.NotFound, TestContext.Current.CancellationToken);

		Assert.Equal(expected.Title, actual.Title);
		Assert.Equal(expected.Code, actual.Code);
	}

	[Fact]
	public async Task AssertUmbrellaValidationProblemDetailsAsyncReturnsDeserializedBody()
	{
		var expected = new UmbrellaValidationProblemDetails(new Dictionary<string, string[]>
		{
			["Name"] = ["The Name field is required."]
		})
		{
			Status = StatusCodes.Status422UnprocessableEntity
		};

		using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
		{
			Content = CreateProblemDetailsContent(expected)
		};

		UmbrellaValidationProblemDetails actual = await response.AssertUmbrellaValidationProblemDetailsAsync(
			HttpStatusCode.UnprocessableEntity,
			TestContext.Current.CancellationToken);

		Assert.Equal(expected.Errors, actual.Errors);
	}

	[Fact]
	public async Task AssertConcurrencyStampMismatchAsyncReturnsDeserializedBody()
	{
		var expected = new UmbrellaProblemDetails
		{
			Status = StatusCodes.Status409Conflict,
			Title = "Concurrency Conflict",
			Code = HttpProblemCodes.ConcurrencyStampMismatch
		};

		using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
		{
			Content = CreateProblemDetailsContent(expected)
		};

		UmbrellaProblemDetails actual = await response.AssertConcurrencyStampMismatchAsync(TestContext.Current.CancellationToken);

		Assert.Equal(expected.Code, actual.Code);
	}

	private static JsonContent CreateProblemDetailsContent<TProblemDetails>(TProblemDetails problemDetails)
		=> JsonContent.Create(
			problemDetails,
			mediaType: new MediaTypeHeaderValue("application/problem+json"));
}
