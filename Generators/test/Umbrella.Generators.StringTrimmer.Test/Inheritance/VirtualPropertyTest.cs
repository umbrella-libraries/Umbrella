using Umbrella.Utilities.Text;

namespace Umbrella.Generators.StringTrimmer.Test.Inheritance;

// Base class with virtual string properties
public abstract class BaseClassWithVirtual
{
	public virtual string VirtualBaseStringProperty { get; set; } = "";
	public virtual string? VirtualBaseNullableStringProperty { get; set; }
	public string NonVirtualBaseStringProperty { get; set; } = "";
}

// Middle class overriding some properties
public class MiddleClassWithOverrides : BaseClassWithVirtual
{
	public override string VirtualBaseStringProperty { get; set; } = "";
	public string MiddleStringProperty { get; set; } = "";
	public virtual string? VirtualMiddleStringProperty { get; set; }
}

// Derived class implementing IUmbrellaTrimmable - should trim all inherited properties
// including virtual and overridden ones
public partial class DerivedClassWithVirtual : MiddleClassWithOverrides, IUmbrellaTrimmable
{
	public override string? VirtualMiddleStringProperty { get; set; }
	public string DerivedStringProperty { get; set; } = "";
}