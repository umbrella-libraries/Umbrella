using Umbrella.Utilities.Text;

namespace Umbrella.Generators.StringTrimmer.Test.ManualImplementations;

public class NonPartialManualImplementation : IUmbrellaTrimmable
{
	public string? Name { get; set; }

	public void TrimAllStringProperties()
	{
		Name = Name?.Trim();
	}
}

public partial record PartialManualImplementation : IUmbrellaTrimmable
{
	public string? Name { get; set; }

	public void TrimAllStringProperties()
	{
		Name = Name?.Trim();
	}
}
