using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Http.Constants;

namespace Umbrella.Utilities.Http;

/// <summary>
/// A utility class used to support querying HTTP services.
/// </summary>
public class GenericHttpServiceUtility : IGenericHttpServiceUtility
{
	private readonly ILogger<GenericHttpServiceUtility> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="GenericHttpServiceUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public GenericHttpServiceUtility(ILogger<GenericHttpServiceUtility> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public IDictionary<string, string> CreateSearchQueryParameters<TItem>(int pageNumber = 0, int pageSize = 20, IEnumerable<SortExpression<TItem>>? sorters = null, IEnumerable<FilterExpression<TItem>>? filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.And)
	{
		try
		{
			return CreateSearchQueryParameters(pageNumber, pageSize, sorters?.ToSortExpressionDescriptors(), filters?.ToFilterExpressionDescriptors(), filterCombinator);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { pageNumber, pageSize, sorters = sorters?.ToSortExpressionDescriptors(), filters = filters?.ToFilterExpressionDescriptors(), filterCombinator }))
		{
			throw new UmbrellaException("There has been a problem creating the query parameters from the search parameters.", exc);
		}
	}

	/// <inheritdoc />
	public IDictionary<string, string> CreateSearchQueryParameters(int pageNumber = 0, int pageSize = 20, IEnumerable<SortExpressionDescriptor>? sorters = null, IEnumerable<FilterExpressionDescriptor>? filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.And)
	{
		try
		{
			var parameters = new Dictionary<string, string>();

			int sorterCount = sorters?.Count() ?? 0;
			int filterCount = filters?.Count() ?? 0;

			if ((pageNumber > 0 && pageSize > 0) || sorterCount > 0 || filterCount > 0)
			{
				if (pageNumber > 0 && pageSize > 0)
				{
					parameters.Add("pageNumber", pageNumber.ToString(CultureInfo.InvariantCulture));
					parameters.Add("pageSize", pageSize.ToString(CultureInfo.InvariantCulture));
				}

				if (sorterCount > 0)
					parameters.Add("sorters", UmbrellaStatics.SerializeJson(sorters!));

				if (filterCount > 0)
				{
					parameters.Add("filters", UmbrellaStatics.SerializeJson(filters!));
					parameters.Add("filterCombinator", filterCombinator.ToString());
				}
			}

			return parameters;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { pageNumber, pageSize, sorters, filters, filterCombinator }))
		{
			throw new UmbrellaException("There has been a problem creating the query parameters from the search parameters.", exc);
		}
	}

	/// <inheritdoc />
	public string GetUrlWithParameters(string url, IEnumerable<KeyValuePair<string, string>>? parameters)
	{
		try
		{
			return parameters?.Count() > 0 ? QueryHelpers.AddQueryString(url, parameters) : url;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { url, parameters }))
		{
			throw new UmbrellaException("There has been a problem creating the url with the specified parameters appended.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<(bool processed, IHttpOperationResult<TResult?> result)> ProcessResponseAsync<TResult>(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(response);

		try
		{
			if (response.IsSuccessStatusCode)
			{
				// First check if we have some content
				if (response.Content.Headers.ContentLength > 0)
				{
					TResult? result = response.Content.Headers.ContentType?.MediaType switch
					{
#if NET6_0_OR_GREATER
						"text/plain" when typeof(TResult) == typeof(string) => (TResult)(object)await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false),
						"application/json" => UmbrellaStatics.DeserializeJson<TResult>(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)),
#else
						"text/plain" when typeof(TResult) == typeof(string) => (TResult)(object)await response.Content.ReadAsStringAsync().ConfigureAwait(false),
						"application/json" => UmbrellaStatics.DeserializeJson<TResult>(await response.Content.ReadAsStringAsync().ConfigureAwait(false)),
#endif
						"text/html" => throw new NotSupportedException("HTML responses are not supported and should not be returned by API endpoints. This might indicate an incorrect API url is being used which doesn't exist on the server."),
						_ => throw new NotImplementedException($"Unsupported media type: {response.Content.Headers.ContentType?.MediaType}")
					};

					return (true, new HttpOperationResult<TResult>(result));
				}

				// Now check for a 201 or 204 in cases where we didn't receive content as those are still valid responses.
				// NB: The ProcessResponseAsync below was added after this method. Need to keep this check here to avoid breaking existing apps.
				if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Created)
					return (true, new HttpOperationResult<TResult>(await GetProblemDetailsAsync(response, cancellationToken).ConfigureAwait(false)));
			}

			return default;
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaException("There has been a problem processing the response.", exc);
		}
	}

	/// <inheritdoc />
	public Task<(bool processed, IHttpOperationResult result)> ProcessResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(response);

		try
		{
			if (response.IsSuccessStatusCode)
			{
				if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Created)
					return Task.FromResult((true, HttpOperationResult.Success()));

				throw new NotSupportedException("Only 201 Created and 204 NoContent responses are supported when there is no result from the endpoint.");
			}

			return Task.FromResult<(bool, IHttpOperationResult)>(default);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaException("There has been a problem processing the response.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<HttpProblemDetails?> GetProblemDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(response);

		try
		{
			HttpContentHeaders headers = response.Content.Headers;

			if (!headers.Any() || headers.ContentType?.MediaType?.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase) is not true)
			{
				string defaultMessage = response.StatusCode switch
				{
					HttpStatusCode.Unauthorized => HttpServiceMessages.DefaultUnauthorizedErrorMessage,
					HttpStatusCode.Forbidden => HttpServiceMessages.DefaultForbiddenErrorMessage,
					HttpStatusCode.InternalServerError => HttpServiceMessages.DefaultServerErrorMessage,
					_ => HttpServiceMessages.DefaultUnknownErrorMessage
				};

				return new HttpProblemDetails { Title = "Error", Detail = defaultMessage };
			}

			// TODO: Is it valid to even do this? Should we not just return null if the content type is not application/problem+json?
#if NET6_0_OR_GREATER
			string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
			string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

			return UmbrellaStatics.DeserializeJson<HttpProblemDetails>(json);
		}
		catch (Exception exc) when (_logger.WriteError(exc))
		{
			throw new UmbrellaException("There has been a problem getting the ProblemDetails response.", exc);
		}
	}
}