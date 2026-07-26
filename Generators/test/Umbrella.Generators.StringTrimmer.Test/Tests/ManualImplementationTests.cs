using Umbrella.Generators.StringTrimmer.Test.ManualImplementations;

namespace Umbrella.Generators.StringTrimmer.Test.Tests;

public class ManualImplementationTests
{
	[Fact]
	public void NonPartialManualImplementation_ShouldRemainUsable()
	{
		var model = new NonPartialManualImplementation
		{
			Name = " value "
		};

		model.TrimAllStringProperties();

		Assert.Equal("value", model.Name);
	}

	[Fact]
	public void PartialManualImplementation_ShouldNotReceiveDuplicateGeneratedMethod()
	{
		var model = new PartialManualImplementation
		{
			Name = " value "
		};

		model.TrimAllStringProperties();

		Assert.Equal("value", model.Name);
	}
}
