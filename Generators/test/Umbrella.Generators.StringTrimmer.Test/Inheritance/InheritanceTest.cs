using Umbrella.Utilities.Text;

namespace Umbrella.Generators.StringTrimmer.Test.Inheritance;

// Base class with string properties
public abstract class BaseClass
{
	public string BaseStringProperty { get; set; } = "";
	public string? BaseNullableStringProperty { get; set; }
}

// Middle class with string properties
public class MiddleClass : BaseClass
{
	public string MiddleStringProperty { get; set; } = "";
	public string? MiddleNullableStringProperty { get; set; }
}

// Derived class implementing IUmbrellaTrimmable - should trim all inherited properties
public partial class DerivedClass : MiddleClass, IUmbrellaTrimmable
{
	public string DerivedStringProperty { get; set; } = "";
	public string? DerivedNullableStringProperty { get; set; }
}