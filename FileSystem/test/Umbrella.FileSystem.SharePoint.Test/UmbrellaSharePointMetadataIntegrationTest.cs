using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.Testing;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.TypeConverters;

namespace Umbrella.FileSystem.SharePoint.Test;

/// <summary>Exercises real Graph requests against an explicitly configured SharePoint test library.</summary>
[Trait("Category", "Integration")]
public class UmbrellaSharePointMetadataIntegrationTest
{
	private static CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	public async Task SchemaAndTypedMetadataRoundTripAgainstSharePoint()
	{
		await using var library = TestLibrary.Create();
		await library.InitializeAsync(Token);
		var before = await library.ReadOwnedColumnsAsync(Token);
		await UmbrellaSharePointMetadataSchema.EnsureAsync(library.Options, Token);
		var after = await library.ReadOwnedColumnsAsync(Token);
		Assert.Equal(7, after.Count);
		Assert.Equal(before.Select(x => x.Id).Order(), after.Select(x => x.Id).Order());

		var file = await library.CreateFileAsync("source", Token);
		const string dateLookingText = "2026-10-22T12:00:00+01:00";
		var date = new DateTimeOffset(2026, 10, 22, 11, 0, 0, TimeSpan.Zero);
		await file.SetMetadataValueAsync("Text", dateLookingText, false, Token);
		await file.SetMetadataValueAsync("Multiline", "line one\nline two", false, Token);
		await file.SetMetadataValueAsync("Boolean", true, false, Token);
		await file.SetMetadataValueAsync("Number", 12.5m, false, Token);
		await file.SetMetadataValueAsync("DateTime", date, false, Token);
		await file.SetMetadataValueAsync("Choice", "Approved", false, Token);
		var fresh = await library.ReloadAsync(file, Token);
		Assert.Equal("unset", await fresh.GetMetadataValueAsync("Text", "unset", cancellationToken: Token));
		await file.WriteMetadataChangesAsync(Token);
		fresh = await library.ReloadAsync(file, Token);
		Assert.Equal(dateLookingText, await fresh.GetMetadataValueAsync<string>("text", cancellationToken: Token));
		Assert.Equal("line one\nline two", await fresh.GetMetadataValueAsync<string>("Multiline", cancellationToken: Token));
		Assert.True(await fresh.GetMetadataValueAsync<bool>("Boolean", cancellationToken: Token));
		Assert.Equal(12.5m, await fresh.GetMetadataValueAsync<decimal>("Number", cancellationToken: Token));
		Assert.Equal(date, await fresh.GetMetadataValueAsync<DateTimeOffset>("DateTime", cancellationToken: Token));
		Assert.Equal("Approved", await fresh.GetMetadataValueAsync<string>("Choice", cancellationToken: Token));
		Assert.Equal("fallback", await fresh.GetMetadataValueAsync("Unmapped", "fallback", cancellationToken: Token));

		await library.SetUnrelatedFieldAsync(file, "keep", Token);
		await fresh.RemoveMetadataValueAsync("Text", cancellationToken: Token);
		fresh = await library.ReloadAsync(file, Token);
		Assert.Equal("removed", await fresh.GetMetadataValueAsync("Text", "removed", cancellationToken: Token));
		await fresh.ClearMetadataAsync(false, Token);
		await fresh.SetMetadataValueAsync("Text", "after-clear", false, Token);
		await fresh.WriteMetadataChangesAsync(Token);
		fresh = await library.ReloadAsync(file, Token);
		Assert.Equal("after-clear", await fresh.GetMetadataValueAsync<string>("Text", cancellationToken: Token));
		Assert.Equal("cleared", await fresh.GetMetadataValueAsync("Choice", "cleared", cancellationToken: Token));
		Assert.Null(await fresh.GetMetadataValueAsync<decimal?>("Number", cancellationToken: Token));
		Assert.Equal("keep", await library.GetUnrelatedFieldAsync(file, Token));
	}

	[Fact]
	public async Task IndependentWritesAndCopyMovePreservePersistedMetadata()
	{
		await using var library = TestLibrary.Create();
		await library.InitializeAsync(Token);
		var file = await library.CreateFileAsync("source", Token);
		await file.SetMetadataValueAsync("Text", "original", cancellationToken: Token);
		var first = await library.ReloadAsync(file, Token);
		var second = await library.ReloadAsync(file, Token);
		// Populate both session caches before either writer updates the item.
		Assert.Equal("original", await first.GetMetadataValueAsync<string>("Text", cancellationToken: Token));
		Assert.Equal("original", await second.GetMetadataValueAsync<string>("Text", cancellationToken: Token));
		await first.SetMetadataValueAsync("Text", "first", cancellationToken: Token);
		await second.SetMetadataValueAsync("Number", 42, cancellationToken: Token);
		var fresh = await library.ReloadAsync(file, Token);
		Assert.Equal("first", await fresh.GetMetadataValueAsync<string>("Text", cancellationToken: Token));
		Assert.Equal(42, await fresh.GetMetadataValueAsync<int>("Number", cancellationToken: Token));
		await second.SetMetadataValueAsync("Text", "last", cancellationToken: Token);
		await second.SetMetadataValueAsync("Text", "pending", false, Token);
		var copy = await second.CopyAsync(library.FilePath("copy"), Token);
		Assert.Equal("last", await copy.GetMetadataValueAsync<string>("Text", cancellationToken: Token));
		Assert.Equal(42, await copy.GetMetadataValueAsync<int>("Number", cancellationToken: Token));
		var moved = await second.MoveAsync(library.FilePath("moved"), Token);
		Assert.Null(await library.Provider.GetAsync(second.SubPath, Token));
		Assert.Equal("last", await moved.GetMetadataValueAsync<string>("Text", cancellationToken: Token));
		Assert.True(await moved.DeleteAsync(Token));
		Assert.Null(await library.Provider.GetAsync(moved.SubPath, Token));
	}

	private sealed class TestLibrary : IAsyncDisposable
	{
		private readonly GraphServiceClient _graph;
		private readonly string _prefix = "um" + Guid.NewGuid().ToString("N")[..20];
		private readonly HashSet<string> _files = new(StringComparer.Ordinal);
		private readonly HashSet<string> _columnNames = new(StringComparer.Ordinal);
		private string? _driveId;
		private string? _listId;
		internal UmbrellaSharePointFileStorageProviderOptions Options { get; }
		internal UmbrellaSharePointFileStorageProvider Provider { get; }

		private TestLibrary(IConfiguration config)
		{
			string Required(string key) => !string.IsNullOrWhiteSpace(config["SharePoint:" + key])
				? config["SharePoint:" + key]!
				: throw new InvalidOperationException($"Live SharePoint tests require SharePoint:{key}.");
			string siteId = Required("SiteId");
			string driveName = Required("DriveName");
			_graph = new GraphServiceClient(new ClientSecretCredential(Required("TenantId"), Required("ClientId"), Required("ClientSecret")));
			Options = new()
			{
				SiteId = siteId,
				DriveName = driveName,
				GraphServiceClient = _graph,
				AllowUnhandledFileAuthorizationChecks = true,
				SubPathTranslator = path => _prefix + "-" + Path.GetFileName(path)
			};
			foreach (var type in Enum.GetValues<SharePointMetadataColumnType>())
			{
				string key = type == SharePointMetadataColumnType.MultilineText ? "Multiline" : type.ToString();
				Options.MetadataColumnMappings[key] = new()
				{
					ColumnName = _prefix + key,
					ColumnType = type,
					Choices = type == SharePointMetadataColumnType.Choice ? ["Draft", "Approved"] : []
				};
			}

			Options.MetadataColumnMappings["Unrelated"] = new() { ColumnName = _prefix + "Unrelated" };
			_columnNames.UnionWith(Options.MetadataColumnMappings.Values.Select(x => x.ColumnName));
			Provider = new(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(),
				new GenericTypeConverter(NullLogger<GenericTypeConverter>.Instance), new UmbrellaFileAuthorizationHandlerRegistry([]));
		}

		internal static TestLibrary Create()
		{
			var config = SharePointTestConfiguration.Create<UmbrellaSharePointMetadataIntegrationTest>();
			Assert.SkipUnless(string.Equals(config["SharePoint:RunIntegrationTests"], "true", StringComparison.OrdinalIgnoreCase),
				"Set SharePoint:RunIntegrationTests=true and configure a SharePoint test library to run live integration tests.");
			return new TestLibrary(config);
		}

		internal async Task InitializeAsync(CancellationToken cancellationToken)
		{
			var request = _graph.Sites[Options.SiteId].Drives;
			var page = await request.GetAsync(cancellationToken: cancellationToken);
			while (page is not null)
			{
				_driveId = page.Value?.SingleOrDefault(x => string.Equals(x.Name, Options.DriveName, StringComparison.OrdinalIgnoreCase))?.Id;
				if (_driveId is not null || string.IsNullOrEmpty(page.OdataNextLink))
					break;
				page = await request.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: cancellationToken);
			}

			Assert.NotNull(_driveId);
			var list = await _graph.Drives[_driveId].List.GetAsync(cancellationToken: cancellationToken);
			_listId = Assert.IsType<string>(list?.Id);
			await UmbrellaSharePointMetadataSchema.EnsureAsync(Options, cancellationToken);
			_ = Options.MetadataColumnMappings.Remove("Unrelated");
			Provider.InitializeOptions(Options);
		}

		internal string FilePath(string name)
		{
			string path = "/" + name + ".txt";
			_ = _files.Add(path);
			return path;
		}

		internal async Task<IUmbrellaFileInfo> CreateFileAsync(string name, CancellationToken cancellationToken)
		{
			var file = await Provider.CreateAsync(FilePath(name), cancellationToken);
			await file.WriteFromByteArrayAsync([1, 2, 3], cancellationToken: cancellationToken);
			return file;
		}

		internal async Task<IUmbrellaFileInfo> ReloadAsync(IUmbrellaFileInfo file, CancellationToken cancellationToken)
			=> await Provider.GetAsync(file.SubPath, cancellationToken) ?? throw new InvalidOperationException("Test file disappeared.");

		internal async Task SetUnrelatedFieldAsync(IUmbrellaFileInfo file, string value, CancellationToken cancellationToken)
		{
			var item = await _graph.Drives[_driveId].Root.ItemWithPath(Options.SubPathTranslator!(file.SubPath))
				.GetAsync(config => config.QueryParameters.Select = ["sharepointIds"], cancellationToken: cancellationToken);
			string itemId = Assert.IsType<string>(item?.SharepointIds?.ListItemId);
			_ = await _graph.Sites[Options.SiteId].Lists[_listId].Items[itemId].Fields
				.PatchAsync(new FieldValueSet { AdditionalData = new Dictionary<string, object> { [_prefix + "Unrelated"] = value } }, cancellationToken: cancellationToken);
		}

		internal async Task<string?> GetUnrelatedFieldAsync(IUmbrellaFileInfo file, CancellationToken cancellationToken)
		{
			var item = await _graph.Drives[_driveId].Root.ItemWithPath(Options.SubPathTranslator!(file.SubPath))
				.GetAsync(config => config.QueryParameters.Select = ["sharepointIds"], cancellationToken: cancellationToken);
			string itemId = Assert.IsType<string>(item?.SharepointIds?.ListItemId);
			var fields = await _graph.Sites[Options.SiteId].Lists[_listId].Items[itemId].Fields.GetAsync(cancellationToken: cancellationToken);
			return fields?.AdditionalData[_prefix + "Unrelated"]?.ToString();
		}

		internal async Task<List<ColumnDefinition>> ReadOwnedColumnsAsync(CancellationToken cancellationToken)
		{
			var columns = new List<ColumnDefinition>();
			var request = _graph.Sites[Options.SiteId].Lists[_listId].Columns;
			var page = await request.GetAsync(cancellationToken: cancellationToken);
			while (page is not null)
			{
				columns.AddRange((page.Value ?? []).Where(x => x.Name is not null && _columnNames.Contains(x.Name)));
				if (string.IsNullOrEmpty(page.OdataNextLink))
					break;
				page = await request.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: cancellationToken);
			}

			return columns;
		}

		[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Cleanup attempts every owned resource and rethrows collected failures as an AggregateException.")]
		public async ValueTask DisposeAsync()
		{
			// Cleanup has its own deadline so a canceled test can still remove its resources.
			using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(2));
			var failures = new List<Exception>();
			try
			{
				foreach (string path in _files)
				{
					try
					{
						await _graph.Drives[_driveId].Root.ItemWithPath(Options.SubPathTranslator!(path)).DeleteAsync(cancellationToken: cleanup.Token);
					}
					catch (ApiException exception) when (exception.ResponseStatusCode == 404)
					{
						// Deleted or moved by the test, or upload failed before creating it.
					}
					catch (Exception exception)
					{
						failures.Add(exception);
					}
				}

				if (_listId is not null)
				{
					foreach (var column in await ReadOwnedColumnsAsync(cleanup.Token))
					{
						try
						{
							await _graph.Sites[Options.SiteId].Lists[_listId].Columns[column.Id].DeleteAsync(cancellationToken: cleanup.Token);
						}
						catch (Exception exception)
						{
							failures.Add(exception);
						}
					}
				}
			}
			finally
			{
				Provider.Dispose();
				_graph.Dispose();
			}

			if (failures.Count > 0)
				throw new AggregateException($"Failed to clean up SharePoint integration-test resources with prefix '{_prefix}'.", failures);
		}
	}
}
