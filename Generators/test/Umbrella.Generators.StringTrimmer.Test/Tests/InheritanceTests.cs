using Umbrella.Generators.StringTrimmer.Test.Inheritance;

namespace Umbrella.Generators.StringTrimmer.Test.Tests;

public class InheritanceTests
{
	[Fact]
	public void DerivedClass_TrimAllStringProperties_TrimsAllInheritedProperties()
	{
		// Arrange
		var derivedClass = new DerivedClass
		{
			// Base class properties
			BaseStringProperty = "  BaseValue  ",
			BaseNullableStringProperty = "  BaseNullableValue  ",

			// Middle class properties
			MiddleStringProperty = "  MiddleValue  ",
			MiddleNullableStringProperty = "  MiddleNullableValue  ",

			// Derived class properties
			DerivedStringProperty = "  DerivedValue  ",
			DerivedNullableStringProperty = "  DerivedNullableValue  "
		};

		// Act
		derivedClass.TrimAllStringProperties();

		// Assert
		// Base class properties
		Assert.Equal("BaseValue", derivedClass.BaseStringProperty);
		Assert.Equal("BaseNullableValue", derivedClass.BaseNullableStringProperty);

		// Middle class properties
		Assert.Equal("MiddleValue", derivedClass.MiddleStringProperty);
		Assert.Equal("MiddleNullableValue", derivedClass.MiddleNullableStringProperty);

		// Derived class properties
		Assert.Equal("DerivedValue", derivedClass.DerivedStringProperty);
		Assert.Equal("DerivedNullableValue", derivedClass.DerivedNullableStringProperty);
	}
}