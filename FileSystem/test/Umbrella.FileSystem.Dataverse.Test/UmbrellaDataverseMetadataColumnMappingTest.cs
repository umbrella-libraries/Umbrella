using Xunit;

namespace Umbrella.FileSystem.Dataverse.Test;

public class UmbrellaDataverseMetadataColumnMappingTest
{
	[Fact]
	public void Validate_Throws_WhenColumnNameIsEmpty()
	{
		var mapping = new DataverseMetadataColumnMapping { ColumnName = "" };

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(mapping.Validate));
	}

	[Fact]
	public void Validate_Throws_WhenLookupTypeHasNoLookupTableName()
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "regardingobjectid",
			ColumnType = DataverseMetadataColumnType.Lookup,
		};

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(mapping.Validate));
	}

	[Fact]
	public void Validate_Throws_WhenOwnerTypeHasNoLookupTableName()
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "ownerid",
			ColumnType = DataverseMetadataColumnType.Owner,
		};

		Assert.IsAssignableFrom<ArgumentException>(Record.Exception(mapping.Validate));
	}

	[Fact]
	public void Validate_DoesNotThrow_ForTextType()
	{
		var mapping = new DataverseMetadataColumnMapping { ColumnName = "subject" };

		var ex = Record.Exception(mapping.Validate);

		Assert.Null(ex);
	}

	[Fact]
	public void Validate_DoesNotThrow_ForBooleanType()
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "isprivate",
			ColumnType = DataverseMetadataColumnType.Boolean,
		};

		var ex = Record.Exception(mapping.Validate);

		Assert.Null(ex);
	}

	[Fact]
	public void Validate_DoesNotThrow_ForLookupTypeWithLookupTableName()
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "regardingobjectid",
			ColumnType = DataverseMetadataColumnType.Lookup,
			LookupTableName = "contact",
		};

		var ex = Record.Exception(mapping.Validate);

		Assert.Null(ex);
	}

	[Fact]
	public void Validate_DoesNotThrow_ForOwnerTypeWithLookupTableName()
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "ownerid",
			ColumnType = DataverseMetadataColumnType.Owner,
			LookupTableName = "systemuser",
		};

		var ex = Record.Exception(mapping.Validate);

		Assert.Null(ex);
	}

	[Theory]
	[InlineData(DataverseMetadataColumnType.Text)]
	[InlineData(DataverseMetadataColumnType.Boolean)]
	[InlineData(DataverseMetadataColumnType.Integer)]
	[InlineData(DataverseMetadataColumnType.Decimal)]
	[InlineData(DataverseMetadataColumnType.DateTime)]
	public void DefaultColumnType_IsText_ForNonReferenceTypes(DataverseMetadataColumnType columnType)
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "somecolumn",
			ColumnType = columnType,
		};

		Assert.Equal(columnType, mapping.ColumnType);
		Assert.Null(mapping.LookupTableName);
	}

	[Theory]
	[InlineData(DataverseMetadataColumnType.Lookup, "contact")]
	[InlineData(DataverseMetadataColumnType.Owner, "systemuser")]
	public void LookupTableName_IsPreserved_ForReferenceTypes(DataverseMetadataColumnType columnType, string lookupTableName)
	{
		var mapping = new DataverseMetadataColumnMapping
		{
			ColumnName = "somecolumn",
			ColumnType = columnType,
			LookupTableName = lookupTableName,
		};

		Assert.Equal(columnType, mapping.ColumnType);
		Assert.Equal(lookupTableName, mapping.LookupTableName);
	}
}
