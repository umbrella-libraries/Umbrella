using Microsoft.PowerPlatform.Dataverse.Client;
using Moq;

namespace Umbrella.FileSystem.Dataverse.Test;

public class UmbrellaDataverseFileStorageProviderOptionsTest
{
	private static UmbrellaDataverseFileStorageProviderOptions BuildValidOptions()
	{
		var mockClient = new Mock<IOrganizationServiceAsync2>();

		return new UmbrellaDataverseFileStorageProviderOptions
		{
			DataverseClient = mockClient.Object,
			TableName = "note",
			IdColumnName = "noteid",
			DataColumnName = "notetext",
			FileNameColumnName = "filename",
		};
	}

	[Fact]
	public void Sanitize_TrimsAllStringProperties()
	{
		var options = BuildValidOptions();
		options.TableName = "  note  ";
		options.IdColumnName = " noteid ";
		options.DataColumnName = " notetext ";
		options.FileNameColumnName = " filename ";

		options.Sanitize();

		Assert.Equal("note", options.TableName);
		Assert.Equal("noteid", options.IdColumnName);
		Assert.Equal("notetext", options.DataColumnName);
		Assert.Equal("filename", options.FileNameColumnName);
	}

	[Fact]
	public void Validate_Throws_WhenDataverseClientIsNull()
	{
		var options = BuildValidOptions();
		options.DataverseClient = null!;

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Validate_Throws_WhenTableNameIsNullOrWhiteSpace(string? value)
	{
		var options = BuildValidOptions();
		options.TableName = value!;

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Validate_Throws_WhenIdColumnNameIsNullOrWhiteSpace(string? value)
	{
		var options = BuildValidOptions();
		options.IdColumnName = value!;

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Validate_Throws_WhenDataColumnNameIsNullOrWhiteSpace(string? value)
	{
		var options = BuildValidOptions();
		options.DataColumnName = value!;

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void Validate_Throws_WhenFileNameColumnNameIsNullOrWhiteSpace(string? value)
	{
		var options = BuildValidOptions();
		options.FileNameColumnName = value!;

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Fact]
	public void Validate_Throws_WhenLookupMappingHasNoLookupTableName()
	{
		var options = BuildValidOptions();
		options.MetadataColumnMappings["Contact"] = new DataverseMetadataColumnMapping
		{
			ColumnName = "regardingobjectid",
			ColumnType = DataverseMetadataColumnType.Lookup,
			LookupTableName = null, // missing
		};

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Fact]
	public void Validate_Throws_WhenOwnerMappingHasNoLookupTableName()
	{
		var options = BuildValidOptions();
		options.MetadataColumnMappings["Owner"] = new DataverseMetadataColumnMapping
		{
			ColumnName = "ownerid",
			ColumnType = DataverseMetadataColumnType.Owner,
			LookupTableName = null, // missing
		};

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(options.Validate));
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenAllPropertiesAreValid()
	{
		var options = BuildValidOptions();
		options.MetadataColumnMappings["Title"] = new DataverseMetadataColumnMapping
		{
			ColumnName = "subject",
			ColumnType = DataverseMetadataColumnType.Text,
		};
		options.MetadataColumnMappings["Owner"] = new DataverseMetadataColumnMapping
		{
			ColumnName = "ownerid",
			ColumnType = DataverseMetadataColumnType.Owner,
			LookupTableName = "systemuser",
		};

		var ex = Record.Exception(options.Validate);

		Assert.Null(ex);
	}
}
