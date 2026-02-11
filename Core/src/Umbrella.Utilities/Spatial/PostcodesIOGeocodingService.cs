
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Spatial.Abstractions;

namespace Umbrella.Utilities.Spatial;

/// <summary>
/// A service that can be used to geocode one or more postcodes or <see cref="GeoLocation"/> instances and return data about each location
/// using the open source https://api.postcodes.io API.
/// </summary>
/// <seealso cref="IGeocodingService" />
public class PostcodesIOGeocodingService : IGeocodingService
{
	private class PostcodeLookupResultWrapper
	{
		public PostcodeLookupResult? Result { get; set; }
	}

	private class PostcodeLookupPartialMapResultsWrapper
	{
		public PostcodeLookupResult[]? Result { get; set; }
	}

	private class PostcodeLookupResult
	{
		public double Latitude { get; set; }
		public double Longitude { get; set; }
		public string? Parish { get; set; }
		public string? Country { get; set; }

		[JsonPropertyName("admin_district")]
		public string? AdminDistrict { get; set; }
	}

	private class BulkPostcodeLookupRequest
	{
		[JsonPropertyName("postcodes")]
		public IEnumerable<string> Postcodes { get; set; } = [];
	}

	private class BulkPostcodeLookupResult
	{
		public class BulkPostcodeLookupInnerResult
		{
			public string? Query { get; set; }
			public PostcodeLookupResult? Result { get; set; }
		}

		public IReadOnlyCollection<BulkPostcodeLookupInnerResult>? Result { get; set; }
	}

	private class BulkReverseGeocodingRequest
	{
		[JsonPropertyName("geolocations")]
		public IEnumerable<ReverseGeocodingRequest> GeoLocations { get; set; } = [];
	}

	private readonly struct ReverseGeocodingRequest
	{
		public ReverseGeocodingRequest(double latitude, double longitude)
		{
			Latitude = latitude;
			Longitude = longitude;
			Radius = 100;
			Limit = 1;
		}

		[JsonPropertyName("latitude")]
		public double Latitude { get; }

		[JsonPropertyName("longitude")]
		public double Longitude { get; }

		[JsonPropertyName("radius")]
		public int Radius { get; }

		[JsonPropertyName("limit")]
		public int Limit { get; }
	}

	private class ReverseGeocodingResultWrapper
	{
		public IReadOnlyCollection<ReverseGeocodingResult>? Result { get; set; }
	}

	private class ReverseGeocodingResult
	{
		public string? Postcode { get; set; }
		public string? Parish { get; set; }
		public string? Country { get; set; }

		[JsonPropertyName("admin_district")]
		public string? AdminDistrict { get; set; }
	}

	private class BulkReverseGeocodingResult
	{
		public class BulkReverseGeocodingInnerResult
		{
			public class BulkReverseGeocodingQuery
			{
				public double Latitude { get; set; }
				public double Longitude { get; set; }
			}

			public BulkReverseGeocodingQuery? Query { get; set; }
			public IReadOnlyCollection<ReverseGeocodingResult>? Result { get; set; }
		}

		public IReadOnlyCollection<BulkReverseGeocodingInnerResult>? Result { get; set; }
	}

	private readonly ILogger _logger;
	private readonly HttpClient _httpClient;
	private readonly string _apiUrl = "https://api.postcodes.io";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostcodesIOGeocodingService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="httpClient">The HTTP client.</param>
	public PostcodesIOGeocodingService(
		ILogger<PostcodesIOGeocodingService> logger,
		HttpClient httpClient)
	{
		_logger = logger;
		_httpClient = httpClient;
	}

	/// <inheritdoc />
	public async Task<(bool success, GeocodingResult? result)> GetGeocodingDataItemByGeoLocationAsync(GeoLocation geoLocation, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var queryParameters = new Dictionary<string, string>
			{
				["lat"] = geoLocation.Latitude.ToString(CultureInfo.InvariantCulture),
				["lon"] = geoLocation.Longitude.ToString(CultureInfo.InvariantCulture),
				["limit"] = "1",
				["radius"] = "1000"
			};

			string url = QueryHelpers.AddQueryString(_apiUrl + "/postcodes", queryParameters);

			HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.Equals("application/json", StringComparison.Ordinal) is true)
			{
				var resultWrapper = await response.Content.ReadFromJsonAsync<ReverseGeocodingResultWrapper>(cancellationToken: cancellationToken).ConfigureAwait(false);

				if (resultWrapper?.Result?.Count > 0)
				{
					var result = resultWrapper.Result.First();

					var dataItem = new GeocodingResult
					{
						Locality = SanitizeParishValue(result.Parish) ?? "",
						Location = geoLocation,
						Postcode = result.Postcode ?? "",
						WiderLocality = result.AdminDistrict ?? "",
						Country = result.Country ?? ""
					};

					return (true, dataItem);
				}
			}

			return default;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { geoLocation }))
		{
			throw new UmbrellaException("There was a problem lookup up the data for the specified geolocation.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<(bool success, IReadOnlyCollection<GeocodingResult>? results)> GetGeocodingDataItemsByGeoLocationsAsync(IEnumerable<GeoLocation> geoLocations, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(geoLocations);

		if (geoLocations.Count() > 100)
			throw new ArgumentOutOfRangeException(nameof(geoLocations), "A maximum of 100 items is allowed.");

		try
		{
			var lstCleanedLocation = geoLocations.Distinct().Select(x => new ReverseGeocodingRequest(x.Latitude, x.Longitude));

			var bulkRequest = new BulkReverseGeocodingRequest
			{
				GeoLocations = lstCleanedLocation
			};

			HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_apiUrl}/postcodes", bulkRequest, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.Equals("application/json", StringComparison.Ordinal) is true)
			{
				var result = await response.Content.ReadFromJsonAsync<BulkReverseGeocodingResult>(cancellationToken: cancellationToken).ConfigureAwait(false);

				if (result?.Result?.Count > 0)
				{
					var lstResult = new List<GeocodingResult>();

					foreach (var item in result.Result)
					{
						if (item.Query is null || item.Result is null || item.Result.Count is 0)
							continue;

						var postCodeResult = item.Result.First();

						var dataItem = new GeocodingResult
						{
							Locality = SanitizeParishValue(postCodeResult.Parish) ?? "",
							Location = new GeoLocation(item.Query.Latitude, item.Query.Longitude),
							Postcode = postCodeResult.Postcode ?? "",
							WiderLocality = postCodeResult.AdminDistrict ?? "",
							Country = postCodeResult.Country ?? ""
						};

						lstResult.Add(dataItem);
					}

					return (true, lstResult);
				}
			}

			return default;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { geoLocations }))
		{
			throw new UmbrellaException("There was a problem lookup up the data for the specified geoLocations.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<(bool success, GeocodingResult? result)> GetGeocodingDataItemByPostcodeAsync(string postcode, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(postcode, nameof(postcode));

		try
		{
			string cleanedPostcode = postcode.TrimToUpperInvariant();

			string encodedPostcode = UrlEncoder.Default.Encode(cleanedPostcode);

			string url = $"{_apiUrl}/postcodes/{encodedPostcode}";

			HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.Equals("application/json", StringComparison.Ordinal) is true)
			{
				var resultWrapper = await response.Content.ReadFromJsonAsync<PostcodeLookupResultWrapper>(cancellationToken: cancellationToken).ConfigureAwait(false);

				PostcodeLookupResult? result = resultWrapper?.Result;

				if (result is not null)
				{
					var dataItem = new GeocodingResult
					{
						Locality = SanitizeParishValue(result.Parish) ?? "",
						Location = new GeoLocation(result.Latitude, result.Longitude),
						Postcode = cleanedPostcode,
						WiderLocality = result.AdminDistrict ?? "",
						Country = result.Country ?? ""
					};

					return (true, dataItem);
				}
			}

			return default;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { postcode }))
		{
			throw new UmbrellaException("There was a problem lookup up the data for the specified postcode.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<(bool success, GeocodingResult? result)> GetGeocodingDataItemByPartialPostcodeAsync(string partialPostcode, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(partialPostcode, nameof(partialPostcode));

		try
		{
			string cleanedPostcode = partialPostcode.TrimToUpperInvariant();

			string encodedPostcode = UrlEncoder.Default.Encode(cleanedPostcode);

			string url = $"{_apiUrl}/postcodes?q={encodedPostcode}";

			HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.Equals("application/json", StringComparison.Ordinal) is true)
			{
				var resultWrapper = await response.Content.ReadFromJsonAsync<PostcodeLookupPartialMapResultsWrapper>(cancellationToken: cancellationToken).ConfigureAwait(false);

				PostcodeLookupResult[]? result = resultWrapper?.Result;

				if (result is not null && result.Length > 0)
				{
					var firstMatch = result[0];

					var dataItem = new GeocodingResult
					{
						Locality = SanitizeParishValue(firstMatch.Parish) ?? "",
						Location = new GeoLocation(firstMatch.Latitude, firstMatch.Longitude),
						Postcode = cleanedPostcode,
						WiderLocality = firstMatch.AdminDistrict ?? "",
						Country = firstMatch.Country ?? ""
					};

					return (true, dataItem);
				}
			}

			return default;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { partialPostcode }))
		{
			throw new UmbrellaException("There was a problem lookup up the data for the specified partial postcode.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<(bool success, IReadOnlyCollection<GeocodingResult>? results)> GetGeocodingDataItemsByPostcodesAsync(IEnumerable<string> postcodes, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (postcodes.Count() > 100)
			throw new ArgumentOutOfRangeException(nameof(postcodes), "A maximum of 100 items is allowed.");

		try
		{
			IEnumerable<string> lstCleanedPostcode = postcodes.Select(x => x?.TrimToUpperInvariant()).Where(x => !string.IsNullOrEmpty(x)).Distinct()!;

			var model = new BulkPostcodeLookupRequest
			{
				Postcodes = lstCleanedPostcode
			};

			using var jsonContent = JsonContent.Create(model);

			HttpResponseMessage response = await _httpClient.PostAsync($"{_apiUrl}/postcodes", jsonContent, cancellationToken).ConfigureAwait(false);

			if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.Equals("application/json", StringComparison.Ordinal) is true)
			{
				var result = await response.Content.ReadFromJsonAsync<BulkPostcodeLookupResult>(cancellationToken: cancellationToken).ConfigureAwait(false);

				if (result?.Result?.Count > 0)
				{
					var lstResult = new List<GeocodingResult>();

					foreach (var item in result.Result)
					{
						if (string.IsNullOrEmpty(item.Query) || item.Result is null)
							continue;

						var dataItem = new GeocodingResult
						{
							Locality = SanitizeParishValue(item.Result.Parish) ?? "",
							Location = new GeoLocation(item.Result.Latitude, item.Result.Longitude),
							Postcode = item.Query ?? "",
							WiderLocality = item.Result.AdminDistrict ?? "",
							Country = item.Result.Country ?? ""
						};

						lstResult.Add(dataItem);
					}

					return (true, lstResult);
				}
			}

			return default;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { postcodes }))
		{
			throw new UmbrellaException("There was a problem lookup up the data for the specified postcodes.", exc);
		}
	}

	private static string? SanitizeParishValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return value;

		int idx = value!.IndexOf(", unparished area", StringComparison.OrdinalIgnoreCase);

		return idx is -1 ? value : value.Substring(0, idx);
	}
}