using System.Text.Json;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.SharePoint.Test;

[Trait("Category", "Component")]
public class UmbrellaSharePointMetadataSchemaTest
{
	private static CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	public async Task MultilineCreationOmitsAppendChangesPropertyAndRemainsCompatibleOnRepeatedSetup()
	{
		using var fixture = new SharePointGraphFixture { EnableAppendOnlyWhenCreationPropertyPresent = true };
		fixture.Map("Notes", SharePointMetadataColumnType.MultilineText);

		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);

		var creation = Assert.Single(fixture.Requests, request => request.Method == "POST");
		using var body = JsonDocument.Parse(creation.Body!);
		var text = body.RootElement.GetProperty("text");
		Assert.True(text.GetProperty("allowMultipleLines").GetBoolean());
		Assert.False(text.TryGetProperty("appendChangesToExistingText", out _));

		fixture.Requests.Clear();
		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		Assert.All(fixture.Requests, request => Assert.Equal("GET", request.Method));
	}

	[Theory]
	[InlineData(SharePointMetadataColumnType.Text, "text")]
	[InlineData(SharePointMetadataColumnType.MultilineText, "text")]
	[InlineData(SharePointMetadataColumnType.Boolean, "boolean")]
	[InlineData(SharePointMetadataColumnType.Number, "number")]
	[InlineData(SharePointMetadataColumnType.DateTime, "dateTime")]
	[InlineData(SharePointMetadataColumnType.Choice, "choice")]
	public async Task CreatesTypedColumnsAndRepeatedSetupIsReadOnly(SharePointMetadataColumnType type, string facet)
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("key", type, "InternalName", type == SharePointMetadataColumnType.Choice ? ["Draft", "Approved"] : []);
		fixture.Options.MetadataColumnMappings["key"] = fixture.Options.MetadataColumnMappings["key"] with { DisplayName = "Friendly name" };
		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		var column = Assert.Single(fixture.Columns);
		Assert.True(column.ContainsKey(facet));
		Assert.Equal("InternalName", column["name"]!.ToString());
		Assert.Equal("Friendly name", column["displayName"]!.ToString());
		Assert.Equal("False", column["required"]!.ToString());
		Assert.Equal("False", column["readOnly"]!.ToString());
		fixture.Requests.Clear();
		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		Assert.All(fixture.Requests, x => Assert.Equal("GET", x.Method));
	}

	[Fact]
	public async Task FollowsAllDriveAndColumnPagesBeforeCreatingAnything()
	{
		using var fixture = new SharePointGraphFixture { PageColumns = true, PageDrives = true };
		fixture.Map("IsVisible", SharePointMetadataColumnType.Boolean, "Visible");
		fixture.Columns.Add(new() { ["name"] = "Visible", ["boolean"] = new { } });
		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		Assert.Contains(fixture.Requests, x => x.Url.Contains("/drives?page=2", StringComparison.Ordinal));
		Assert.Contains(fixture.Requests, x => x.Url.Contains("/columns?page=2", StringComparison.Ordinal));
		Assert.DoesNotContain(fixture.Requests, x => x.Method != "GET");
	}

	[Theory]
	[InlineData("type")]
	[InlineData("readonly")]
	[InlineData("append")]
	[InlineData("appendmultiline")]
	[InlineData("richtext")]
	[InlineData("choices")]
	[InlineData("multichoice")]
	public async Task RejectsIncompatibleColumnsBeforeProvisioningMissingOnes(string mismatch)
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("missing", SharePointMetadataColumnType.Boolean);
		var columnType = mismatch switch
		{
			"choices" or "multichoice" => SharePointMetadataColumnType.Choice,
			"appendmultiline" => SharePointMetadataColumnType.MultilineText,
			_ => SharePointMetadataColumnType.Text
		};
		fixture.Map("existing", columnType,
			"existing", mismatch is "choices" or "multichoice" ? ["Draft"] : []);
		var column = new Dictionary<string, object?> { ["name"] = "existing", ["text"] = new { allowMultipleLines = false, textType = "plain" } };
		switch (mismatch)
		{
			case "type":
				_ = column.Remove("text");
				column["boolean"] = new { };
				break;
			case "readonly": column["readOnly"] = true; break;
			case "append": column["text"] = new { appendChangesToExistingText = true }; break;
			case "appendmultiline": column["text"] = new { allowMultipleLines = true, appendChangesToExistingText = true, textType = "plain" }; break;
			case "richtext": column["text"] = new { textType = "richText" }; break;
			default:
				_ = column.Remove("text");
				column["choice"] = new { displayAs = mismatch == "multichoice" ? "checkBoxes" : "dropDownMenu", choices = new[] { mismatch == "choices" ? "Other" : "Draft" } };
				break;
		}

		fixture.Columns.Add(column);
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token));
		Assert.DoesNotContain(fixture.Requests, x => x.Method != "GET");
	}

	[Theory]
	[InlineData(false, 409)]
	[InlineData(true, 409)]
	[InlineData(false, 400)]
	[InlineData(true, 400)]
	public async Task ConcurrentCreationIsRereadAndValidated(bool incompatible, int status)
	{
		using var fixture = new SharePointGraphFixture { RaceColumn = "Visible", IncompatibleRace = incompatible, RaceStatus = (HttpStatusCode)status };
		fixture.Map("IsVisible", SharePointMetadataColumnType.Boolean, "Visible");
		if (incompatible)
			_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token));
		else
			await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		Assert.Equal(2, fixture.Requests.Count(x => x.Method == "GET" && x.Url.Contains("/columns", StringComparison.Ordinal)));
	}

	[Fact]
	public async Task PartialCreationIsReportedAndCanBeRetried()
	{
		using var fixture = new SharePointGraphFixture { FailColumn = "Second" };
		fixture.Map("First", SharePointMetadataColumnType.Boolean);
		fixture.Map("Second", SharePointMetadataColumnType.Text);
		var exception = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token));
		Assert.Contains("Second", exception.Message, StringComparison.Ordinal);
		_ = Assert.Single(fixture.Columns);
		fixture.FailColumn = null;
		fixture.Requests.Clear();
		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		Assert.Equal(2, fixture.Columns.Count);
		_ = Assert.Single(fixture.Requests, x => x.Method == "POST");
	}

	[Fact]
	public async Task EmptyMappingsAndCancellationDoNotAccessGraph()
	{
		using var fixture = new SharePointGraphFixture();
		await UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, Token);
		fixture.Map("Visible", SharePointMetadataColumnType.Boolean);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UmbrellaSharePointMetadataSchema.EnsureAsync(fixture.Options, cancellation.Token));
		Assert.Empty(fixture.Requests);
	}

	[Theory]
	[InlineData("key")]
	[InlineData("column")]
	[InlineData("display")]
	[InlineData("type")]
	[InlineData("duplicatecolumn")]
	[InlineData("duplicatekey")]
	[InlineData("emptychoices")]
	[InlineData("duplicatechoices")]
	[InlineData("blankchoice")]
	[InlineData("unexpectedchoices")]
	public void InvalidConfigurationIsRejected(string invalid)
	{
		using var fixture = new SharePointGraphFixture();
		fixture.Map("key", SharePointMetadataColumnType.Text, "column");
		switch (invalid)
		{
			case "key": fixture.Map(" ", SharePointMetadataColumnType.Text); break;
			case "column": fixture.Options.MetadataColumnMappings["key"] = new() { ColumnName = " " }; break;
			case "display": fixture.Options.MetadataColumnMappings["key"] = new() { ColumnName = "column", DisplayName = " " }; break;
			case "type": fixture.Map("key", (SharePointMetadataColumnType)999); break;
			case "duplicatecolumn": fixture.Map("other", SharePointMetadataColumnType.Text, "COLUMN"); break;
			case "duplicatekey": fixture.Options.MetadataColumnMappings = new(StringComparer.Ordinal) { ["key"] = new() { ColumnName = "a" }, ["KEY"] = new() { ColumnName = "b" } }; break;
			case "emptychoices": fixture.Map("key", SharePointMetadataColumnType.Choice); break;
			case "duplicatechoices": fixture.Map("key", SharePointMetadataColumnType.Choice, "column", "a", "a"); break;
			case "blankchoice": fixture.Map("key", SharePointMetadataColumnType.Choice, "column", " "); break;
			case "unexpectedchoices": fixture.Map("key", SharePointMetadataColumnType.Text, "column", "a"); break;
		}

		_ = Assert.ThrowsAny<ArgumentException>(fixture.Options.Validate);
		Assert.Empty(fixture.Requests);
	}
}
