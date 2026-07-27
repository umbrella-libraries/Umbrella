using Umbrella.Utilities.Data.Concurrency;
using Umbrella.Utilities.Text;

#pragma warning disable IDE0130
namespace Umbrella.Analyzers
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class UmbrellaAllowMutablePropertyAttribute(string justification) : Attribute
	{
		public string Justification { get; } = justification;
	}
}
#pragma warning restore IDE0130

namespace Umbrella.Generators.StringTrimmer.Test.TrimmingExclusions
{
	public partial record TrimmingExclusionModel : IUmbrellaTrimmable, IConcurrencyStamp
	{
		public string Name { get; set; } = "";

		[UmbrellaDoNotTrim]
		public string Password { get; set; } = "";

		[Umbrella.Analyzers.UmbrellaAllowMutableProperty("Populated after mapping.")]
		public string TechnicalValue { get; set; } = "";

		public string ConcurrencyStamp { get; set; } = "";
	}

	public abstract partial record TrimmableInputBase : IUmbrellaTrimmable
	{
		public string BaseValue { get; set; } = "";
	}

	public partial record TrimmableDerivedInput : TrimmableInputBase, IUmbrellaTrimmable
	{
		public string DerivedValue { get; set; } = "";
	}
}
