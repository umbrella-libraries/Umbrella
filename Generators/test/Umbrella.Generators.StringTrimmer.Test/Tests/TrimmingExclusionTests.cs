using Umbrella.Generators.StringTrimmer.Test.TrimmingExclusions;

namespace Umbrella.Generators.StringTrimmer.Test.Tests;

public class TrimmingExclusionTests
{
	[Fact]
	public void TrimAllStringProperties_SkipsExplicitAndTechnicalExclusions()
	{
		var model = new TrimmingExclusionModel
		{
			Name = "  Name  ",
			Password = "  password  ",
			TechnicalValue = "  technical  ",
			ConcurrencyStamp = "  stamp  "
		};

		model.TrimAllStringProperties();

		Assert.Equal("Name", model.Name);
		Assert.Equal("  password  ", model.Password);
		Assert.Equal("  technical  ", model.TechnicalValue);
		Assert.Equal("  stamp  ", model.ConcurrencyStamp);
	}

	[Fact]
	public void TrimAllStringProperties_SkipsStampReachedViaUpdateResultModel()
	{
		var model = new UpdateResultStampModel
		{
			Name = "  Name  ",
			ConcurrencyStamp = "  stamp  "
		};

		model.TrimAllStringProperties();

		Assert.Equal("Name", model.Name);
		Assert.Equal("  stamp  ", model.ConcurrencyStamp);
	}

	[Fact]
	public void TrimAllStringProperties_SkipsStampReachedViaReadOnlyConcurrencyStamp()
	{
		var model = new DirectReadOnlyStampModel
		{
			Name = "  Name  ",
			ConcurrencyStamp = "  stamp  "
		};

		model.TrimAllStringProperties();

		Assert.Equal("Name", model.Name);
		Assert.Equal("  stamp  ", model.ConcurrencyStamp);
	}

	[Fact]
	public void TrimAllStringProperties_SkipsInitOnlyStamp()
	{
		var model = new InitOnlyStampModel
		{
			Name = "  Name  ",
			ConcurrencyStamp = "  stamp  "
		};

		model.TrimAllStringProperties();

		Assert.Equal("Name", model.Name);
		Assert.Equal("  stamp  ", model.ConcurrencyStamp);
	}

	[Fact]
	public void TrimAllStringProperties_SkipsNestedStampButTrimsNestedName()
	{
		var model = new NestedStampOwnerModel
		{
			OwnerName = "  owner  ",
			Nested = new UpdateResultStampModel
			{
				Name = "  nested  ",
				ConcurrencyStamp = "  stamp  "
			}
		};

		model.TrimAllStringProperties();

		Assert.Equal("owner", model.OwnerName);
		Assert.Equal("nested", model.Nested.Name);
		Assert.Equal("  stamp  ", model.Nested.ConcurrencyStamp);
	}

	[Fact]
	public void TrimAllStringProperties_DerivedDirectImplementationCoversBaseAndDerivedProperties()
	{
		var model = new TrimmableDerivedInput
		{
			BaseValue = "  base  ",
			DerivedValue = "  derived  "
		};

		model.TrimAllStringProperties();

		Assert.Equal("base", model.BaseValue);
		Assert.Equal("derived", model.DerivedValue);
	}
}
