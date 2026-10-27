using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.TypeConverters;

namespace Umbrella.FileSystem.SharePoint.Test;

internal sealed class SharePointGraphFixture : IDisposable
{
	private readonly HttpClient _http;
	private readonly Handler _handler;
	internal GraphServiceClient Graph { get; }
	internal UmbrellaSharePointFileStorageProviderOptions Options { get; }
	internal Dictionary<string, Dictionary<string, object?>> Files { get; } = new(StringComparer.Ordinal);
	internal List<Dictionary<string, object?>> Columns { get; } = [];
	internal List<(string Method, string Url, string? Body)> Requests { get; } = [];
	internal bool FailPatch { get; set; }
	internal string? FailColumn { get; set; }
	internal string? RaceColumn { get; set; }
	internal bool IncompatibleRace { get; set; }
	internal HttpStatusCode RaceStatus { get; set; } = HttpStatusCode.Conflict;
	internal bool PageColumns { get; set; }
	internal bool PageDrives { get; set; }
	internal bool EnableAppendOnlyWhenCreationPropertyPresent { get; set; }
	internal static GenericTypeConverter Converter { get; } = new(NullLogger<GenericTypeConverter>.Instance);

	internal SharePointGraphFixture()
	{
		_handler = new Handler(RespondAsync);
		_http = new HttpClient(_handler, disposeHandler: false);
		Graph = new GraphServiceClient(_http, new AnonymousAuthenticationProvider());
		Options = new()
		{
			SiteId = "site", DriveName = "Documents", GraphServiceClient = Graph,
			AllowUnhandledFileAuthorizationChecks = true,
			SubPathTranslator = path => "physical/" + Path.GetFileName(path)
		};
		Files["source.bin"] = new() { ["Visible"] = true, ["Caption"] = "initial", ["Unrelated"] = "keep" };
	}

	internal void Map(string key, SharePointMetadataColumnType type, string? column = null, params string[] choices)
		=> Options.MetadataColumnMappings[key] = new() { ColumnName = column ?? key, ColumnType = type, Choices = choices };

	internal UmbrellaSharePointFileStorageProvider CreateProvider(IUmbrellaFileAuthorizationHandler? handler = null)
	{
		var provider = new UmbrellaSharePointFileStorageProvider(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), Converter,
			new UmbrellaFileAuthorizationHandlerRegistry(handler is null ? [] : [handler]));
		provider.InitializeOptions(Options);
		return provider;
	}

	private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		string url = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
		string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
		string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
		Requests.Add((request.Method.Method, url, body));
		if (path.EndsWith("/drives", StringComparison.Ordinal))
		{
			if (PageDrives && !url.Contains("page=2", StringComparison.Ordinal))
				return Json(new Dictionary<string, object?> { ["value"] = Array.Empty<object>(), ["@odata.nextLink"] = "https://graph.microsoft.com/v1.0/sites/site/drives?page=2" });
			return Json(new { value = new[] { new { id = "drive", name = "Documents" } } });
		}

		if (path == "/v1.0/drives/drive/list")
			return Json(new { id = "list" });
		if (path.EndsWith("/columns", StringComparison.Ordinal))
		{
			if (request.Method == HttpMethod.Get)
			{
				if (PageColumns && !url.Contains("page=2", StringComparison.Ordinal))
					return Json(new Dictionary<string, object?> { ["value"] = Array.Empty<object>(), ["@odata.nextLink"] = "https://graph.microsoft.com/v1.0/sites/site/lists/list/columns?page=2" });
				return Json(new { value = Columns });
			}

			Assert.Equal(HttpMethod.Post, request.Method);
			var column = JsonSerializer.Deserialize<Dictionary<string, object?>>(body!)!;
			string name = column["name"]!.ToString()!;
			if (EnableAppendOnlyWhenCreationPropertyPresent && column.TryGetValue("text", out object? facet) && facet is JsonElement text
				&& text.TryGetProperty("allowMultipleLines", out JsonElement multiline) && multiline.GetBoolean())
			{
				// SharePoint enables append-only mode when the property is supplied, even as false.
				var returnedText = text.Deserialize<Dictionary<string, object?>>()!;
				returnedText["appendChangesToExistingText"] = text.TryGetProperty("appendChangesToExistingText", out _);
				column["text"] = returnedText;
			}

			if (name == FailColumn)
				return Error(HttpStatusCode.BadRequest);
			if (name == RaceColumn)
			{
				if (IncompatibleRace)
					column["readOnly"] = true;
				Columns.Add(column);
				RaceColumn = null;
				return Error(RaceStatus);
			}

			Columns.Add(column);
			return Json(column, HttpStatusCode.Created);
		}

		if (path.EndsWith("/fields", StringComparison.Ordinal))
		{
			string id = path.Split('/')[^2];
			if (request.Method == HttpMethod.Get)
				return Json(Files[id]);
			Assert.Equal(HttpMethod.Patch, request.Method);
			if (FailPatch)
				return Error(HttpStatusCode.BadRequest);
			using var json = JsonDocument.Parse(body!);
			foreach (var property in json.RootElement.EnumerateObject())
				Files[id][property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.Clone();
			return Json(Files[id]);
		}

		int marker = path.IndexOf("root:/physical/", StringComparison.Ordinal);
		if (marker >= 0)
		{
			string id = path[(marker + "root:/physical/".Length)..].Split(':')[0];
			if (request.Method == HttpMethod.Put)
			{
				_ = Files.TryAdd(id, []);
				return Item(id);
			}

			if (!Files.ContainsKey(id))
				return Error(HttpStatusCode.NotFound);
			if (request.Method == HttpMethod.Delete)
			{
				_ = Files.Remove(id);
				return new(HttpStatusCode.NoContent);
			}

			if (path.EndsWith("/content", StringComparison.Ordinal))
				return new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
			return Item(id);
		}

		throw new InvalidOperationException($"Unexpected Graph request: {request.Method} {url}");
	}

	private static HttpResponseMessage Item(string id) => Json(new { id, size = 3, file = new { mimeType = "application/octet-stream" }, sharepointIds = new { listId = "list", listItemId = id } });
	private static HttpResponseMessage Error(HttpStatusCode status) => Json(new { error = new { code = status.ToString(), message = "Injected Graph error" } }, status);
	private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };

	public void Dispose()
	{
		Graph.Dispose();
		_http.Dispose();
		_handler.Dispose();
	}

	private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
	}
}
