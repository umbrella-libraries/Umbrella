using Umbrella.Generators.StringTrimmer.Test.Inheritance;

namespace Umbrella.Generators.StringTrimmer.Test.Tests;

public class VirtualPropertyTests
{
	[Fact]
	public void DerivedClassWithVirtual_TrimAllStringProperties_TrimsAllInheritedVirtualProperties()
	{
		// Arrange
		var derivedClass = new DerivedClassWithVirtual
		{
			// Base class virtual properties
			VirtualBaseStringProperty = "  VirtualBaseValue  ",
			VirtualBaseNullableStringProperty = "  VirtualBaseNullableValue  ",
			NonVirtualBaseStringProperty = "  NonVirtualBaseValue  ",

			// Middle class properties
			MiddleStringProperty = "  MiddleValue  ",
			VirtualMiddleStringProperty = "  VirtualMiddleValue  ",

			// Derived class properties
			DerivedStringProperty = "  DerivedValue  "
		};

		// Act
		derivedClass.TrimAllStringProperties();

		// Assert
		// Base class properties
		Assert.Equal("VirtualBaseValue", derivedClass.VirtualBaseStringProperty);
		Assert.Equal("VirtualBaseNullableValue", derivedClass.VirtualBaseNullableStringProperty);
		Assert.Equal("NonVirtualBaseValue", derivedClass.NonVirtualBaseStringProperty);

		// Middle class properties
		Assert.Equal("MiddleValue", derivedClass.MiddleStringProperty);
		Assert.Equal("VirtualMiddleValue", derivedClass.VirtualMiddleStringProperty);

		// Derived class properties
		Assert.Equal("DerivedValue", derivedClass.DerivedStringProperty);
	}
}