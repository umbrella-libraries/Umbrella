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
