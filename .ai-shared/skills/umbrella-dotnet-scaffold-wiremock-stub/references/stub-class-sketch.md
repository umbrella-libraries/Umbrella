# Stub class sketch

A stub for a fictional REST catalogue API. The app lists releases with `GET /v1/releases?format=vinyl&page=1&pageSize=6`, then looks up each artist with a JSON `POST /v1/artists/lookup` whose body is `{ "artistId": 101 }`. Adapt the paths, methods, matchers and request parsing to the real client.

```csharp
using System.Net;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IndyRecords.Web.Server.Testing;

/// <summary>A request the app sent to a stub, in the form tests assert on.</summary>
public sealed record StubRequest(string Method, string Path, IReadOnlyDictionary<string, string> Query, string? Body);

/// <summary>
/// A stand-in catalogue API, so tests control what it returns and can check the requests the app sent.
/// </summary>
/// <remarks>
/// A request no stub matches gets a 404, which the app's client treats as a failure. Stubs are shared by every test
/// using the host, so tests that set them belong in <see cref="CatalogueApiCollection"/> and call <see cref="Reset"/> first.
/// </remarks>
public sealed class CatalogueApiStub : IDisposable
{
	private const string ReleasesPath = "/v1/releases";
	private const string ArtistLookupPath = "/v1/artists/lookup";

	private readonly WireMockServer _server = WireMockServer.Start();

	/// <summary>Gets the address to configure the app with.</summary>
	public string Address => _server.Url!;

	/// <summary>Gets the requests the app has sent since the last reset, in order.</summary>
	public IReadOnlyList<StubRequest> Requests =>
		[.. _server.LogEntries
			.Select(x => x.RequestMessage)
			.OfType<WireMock.IRequestMessage>()
			.OrderBy(x => x.DateTime)
			.Select(x => new StubRequest(x.Method, x.Path, ReadQuery(x), x.Body))];

	/// <summary>Removes every stub and forgets the requests received.</summary>
	public void Reset() => _server.Reset();

	/// <summary>Forgets the requests received so far, keeping the stubs.</summary>
	public void ClearRequests() => _server.ResetLogEntries();

	public void GivenReleases(string format, string responseJson)
		=> _server
			.Given(Request.Create().WithPath(ReleasesPath).UsingGet().WithParam("format", format))
			.RespondWith(Json(responseJson));

	public void GivenArtist(int artistId, string responseJson)
		=> _server
			.Given(Request.Create().WithPath(ArtistLookupPath).UsingPost().WithBody(new JsonPartialMatcher($$"""{ "artistId": {{artistId}} }""")))
			.RespondWith(Json(responseJson));

	/// <summary>
	/// Answers a vinyl listing with the responses recorded from the test environment, plus the artist lookup for each
	/// result. The same response is returned whatever the paging, so tests check <see cref="Requests"/>.
	/// </summary>
	public void GivenRecordedVinylReleases()
	{
		GivenReleases("vinyl", ReadRecordedResponse("vinyl-releases.json"));

		foreach (int artistId in RecordedVinylArtistIds)
			GivenArtist(artistId, ReadRecordedResponse($"artist-{artistId}.json"));
	}

	public static IReadOnlyList<int> RecordedVinylArtistIds { get; } = [101, 204, 317];

	public void GivenReleasesFail(HttpStatusCode statusCode)
		=> _server
			.Given(Request.Create().WithPath(ReleasesPath).UsingGet())
			.RespondWith(Response.Create().WithStatusCode(statusCode));

	public void Dispose() => _server.Dispose();

	private static IResponseBuilder Json(string body)
		=> Response.Create().WithStatusCode(HttpStatusCode.OK).WithHeader("Content-Type", "application/json").WithBody(body);

	// Repeated parameters are joined with commas, so tests compare plain strings.
	private static Dictionary<string, string> ReadQuery(WireMock.IRequestMessage request)
		=> request.Query?.ToDictionary(x => x.Key, x => string.Join(",", x.Value), StringComparer.OrdinalIgnoreCase)
			?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	// The recorded responses are copied to the output of every project that references this one.
	private static string ReadRecordedResponse(string fileName)
		=> File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CatalogueApiResponses", fileName));
}

/// <summary>
/// The collection for tests that stub the catalogue API. xUnit only applies a collection definition declared in the
/// test assembly itself, so each test project declares
/// <c>[CollectionDefinition(CatalogueApiCollection.Name, DisableParallelization = true)]</c>.
/// </summary>
public static class CatalogueApiCollection
{
	public const string Name = "Catalogue API";
}
```

When an API sends every operation to one endpoint (one POST URL whose body holds the operation as text), path and parameters no longer tell requests apart. Match a stable fragment of the body text with `WildcardMatcher` instead, and parse the operation out of the body when building the request log, so tests can still assert on what was asked for.

## Factory wiring

```csharp
public CatalogueApiStub CatalogueApi { get; } = new();

protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides()
	=> base.GetConfigurationOverrides().Append(new("CatalogueApi:BaseUrl", CatalogueApi.Address));

public override async ValueTask DisposeAsync()
{
	try
	{
		await base.DisposeAsync();
	}
	finally
	{
		CatalogueApi.Dispose();
	}

	GC.SuppressFinalize(this);
}
```

## Test usage

```csharp
[Collection(CatalogueApiCollection.Name)]
public sealed class CatalogueControllerTests
{
	private readonly IndyRecordsWebApplicationFactory _factory;

	public CatalogueControllerTests(IndyRecordsWebApplicationFactory factory)
	{
		_factory = factory;
		_factory.CatalogueApi.Reset();
		_factory.Errors.Clear();
	}

	[IndyRecordsFact]
	public async Task Search_ReturnsTheRecordedResults_AndSendsTheRequestedPage()
	{
		_factory.CatalogueApi.GivenRecordedVinylReleases();

		using HttpClient client = _factory.CreateClient();
		using HttpResponseMessage response = await client.GetAsync(new Uri("api/catalogue/search?format=vinyl&pageNumber=1&pageSize=6", UriKind.Relative), TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.True(_factory.Errors.Logged.Count is 0, string.Join(Environment.NewLine, _factory.Errors.Logged));

		StubRequest listing = Assert.Single(_factory.CatalogueApi.Requests, x => x.Path == "/v1/releases");
		Assert.Equal("vinyl", listing.Query["format"]);
		Assert.Equal("6", listing.Query["pageSize"]);
	}
}
```
