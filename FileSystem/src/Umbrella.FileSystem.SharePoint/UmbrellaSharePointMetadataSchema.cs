using CommunityToolkit.Diagnostics;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>Explicitly provisions metadata columns in an existing SharePoint document library.</summary>
public static class UmbrellaSharePointMetadataSchema
{
	/// <summary>Validates existing mapped columns and creates missing columns. Never updates or deletes existing schema.</summary>
	/// <param name="options">The target library, Graph client, and column mappings.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <remarks>
	/// Requires Graph schema-management permissions. Call explicitly at startup or deployment, not per file operation.
	/// A failure can leave previously created columns in place; rerunning is safe after correcting the failure.
	/// </remarks>
	public static async Task EnsureAsync(UmbrellaSharePointFileStorageProviderOptions options, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(options);
		options.Validate();
		var mappings = SharePointMetadataColumnMapping.Snapshot(options.MetadataColumnMappings);
		if (mappings.Count == 0)
			return;
		string driveId = await SharePointGraphHelper.ResolveDriveIdAsync(options, cancellationToken).ConfigureAwait(false);
		var list = await options.GraphServiceClient.Drives[driveId].List.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(list?.Id))
			throw new InvalidOperationException($"The list for SharePoint library '{options.DriveName}' could not be resolved.");
		var columns = await ReadColumnsAsync(options, list.Id, cancellationToken).ConfigureAwait(false);
		// Check every existing definition before creating any missing columns.
		foreach (var mapping in mappings.Values)
		{
			if (columns.TryGetValue(mapping.ColumnName, out var column))
				ValidateExisting(mapping, column);
		}

		foreach (var mapping in mappings.Values)
		{
			if (columns.TryGetValue(mapping.ColumnName, out var knownColumn))
			{
				ValidateExisting(mapping, knownColumn);
				continue;
			}

			try
			{
				try
				{
					var created = await options.GraphServiceClient.Sites[options.SiteId].Lists[list.Id].Columns
						.PostAsync(CreateDefinition(mapping), cancellationToken: cancellationToken).ConfigureAwait(false)
						?? throw new InvalidOperationException("SharePoint returned no column definition after creation.");
					ValidateExisting(mapping, created);
				}
				catch (ApiException exception) when (exception.ResponseStatusCode is 400 or 409)
				{
					// Another startup/deployment may have created the column since our read.
					columns = await ReadColumnsAsync(options, list.Id, cancellationToken).ConfigureAwait(false);
					if (!columns.TryGetValue(mapping.ColumnName, out var existing))
						throw;
					ValidateExisting(mapping, existing);
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				throw new UmbrellaFileSystemException($"Could not ensure SharePoint metadata column '{mapping.ColumnName}' in library '{options.DriveName}'. Previously created columns remain in place; setup can be retried.", exception);
			}
		}
	}

	private static async Task<Dictionary<string, ColumnDefinition>> ReadColumnsAsync(UmbrellaSharePointFileStorageProviderOptions options, string listId, CancellationToken cancellationToken)
	{
		var result = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
		var request = options.GraphServiceClient.Sites[options.SiteId].Lists[listId].Columns;
		var page = await request.GetAsync(config => config.QueryParameters.Select =
			["id", "name", "hidden", "readOnly", "text", "boolean", "number", "dateTime", "choice"], cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException("SharePoint returned no column collection.");
		while (true)
		{
			foreach (var column in page.Value ?? [])
			{
				if (!string.IsNullOrWhiteSpace(column.Name))
					result.Add(column.Name, column);
			}

			if (string.IsNullOrEmpty(page.OdataNextLink))
				break;
			page = await request.WithUrl(page.OdataNextLink).GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false)
				?? throw new InvalidOperationException("SharePoint returned no column collection for a continuation page.");
		}

		return result;
	}

	private static ColumnDefinition CreateDefinition(SharePointMetadataColumnMapping mapping)
	{
		var column = new ColumnDefinition
		{
			Name = mapping.ColumnName,
			DisplayName = mapping.DisplayName ?? mapping.ColumnName,
			Required = false,
			ReadOnly = false
		};
		switch (mapping.ColumnType)
		{
			case SharePointMetadataColumnType.Text:
			case SharePointMetadataColumnType.MultilineText:
				// SharePoint enables append-only multiline text when appendChangesToExistingText
				// is supplied, even as false. Omit it to retain replacement-text semantics.
				column.Text = new TextColumn { AllowMultipleLines = mapping.ColumnType == SharePointMetadataColumnType.MultilineText, TextType = "plain" };
				break;
			case SharePointMetadataColumnType.Boolean:
				column.Boolean = new BooleanColumn();
				break;
			case SharePointMetadataColumnType.Number:
				column.Number = new NumberColumn { DecimalPlaces = "automatic", DisplayAs = "number" };
				break;
			case SharePointMetadataColumnType.DateTime:
				column.DateTime = new DateTimeColumn { Format = "dateTime", DisplayAs = "default" };
				break;
			case SharePointMetadataColumnType.Choice:
				column.Choice = new ChoiceColumn { Choices = mapping.Choices.ToList(), AllowTextEntry = false, DisplayAs = "dropDownMenu" };
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(mapping));
		}

		return column;
	}

	private static void ValidateExisting(SharePointMetadataColumnMapping mapping, ColumnDefinition column)
	{
		bool compatible = string.Equals(mapping.ColumnName, column.Name, StringComparison.OrdinalIgnoreCase)
			&& column.ReadOnly != true && (mapping.ColumnType switch
			{
				SharePointMetadataColumnType.Text or SharePointMetadataColumnType.MultilineText => column.Text is { } text
					&& (text.AllowMultipleLines == true) == (mapping.ColumnType == SharePointMetadataColumnType.MultilineText)
					&& text.AppendChangesToExistingText != true && (text.TextType is null or "plain"),
				SharePointMetadataColumnType.Boolean => column.Boolean is not null,
				SharePointMetadataColumnType.Number => column.Number is { DisplayAs: null or "number" },
				SharePointMetadataColumnType.DateTime => column.DateTime is { Format: "dateTime" },
				SharePointMetadataColumnType.Choice => column.Choice is { } choice && choice.AllowTextEntry != true
					&& choice.DisplayAs is "dropDownMenu" or "radioButtons"
					&& new HashSet<string>(choice.Choices ?? [], StringComparer.Ordinal).SetEquals(mapping.Choices),
				_ => false
			});
		if (!compatible)
			throw new InvalidOperationException($"SharePoint column '{mapping.ColumnName}' is incompatible with metadata type '{mapping.ColumnType}' or is read-only. Existing columns are never modified by schema setup.");
	}
}
