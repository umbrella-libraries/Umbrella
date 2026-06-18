using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.Loader;
using Umbrella.DataAnnotations;
using Umbrella.TypeScript.Annotations.Attributes;
using Umbrella.TypeScript.Annotations.Enumerations;

namespace Umbrella.TypeScript.Test;

public class TypeScriptGeneratorTest
{
	[Fact]
	public void GenerateAllTest()
	{
		var testAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Umbrella.TypeScript.Test"));

		TypeScriptGenerator generator = new TypeScriptGenerator([testAssembly])
			.IncludeStandardGenerators()
			.IncludeKnockoutGenerators(true);

		string output = generator.GenerateAll(true, true, TypeScriptPropertyMode.Model);

		Assert.NotNull(output);
	}

	[Fact]
	public void GenerateAllUsingOnlyNamedAssembliesTest()
	{
		_ = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Umbrella.TypeScript.Test"));

		TypeScriptGenerator generator = new TypeScriptGenerator("Umbrella.TypeScript.Test")
			.IncludeStandardGenerators()
			.IncludeKnockoutGenerators(false);

		string output = generator.GenerateAll(true, true, TypeScriptPropertyMode.Model);

		Assert.NotNull(output);
	}
}

[TypeScriptEnum]
public enum TestEnum
{
	None,
	Value1,
	Value2
}

[TypeScriptModel(TypeScriptOutputModelType.Class | TypeScriptOutputModelType.KnockoutClass)]
public class TestClass : ITestInterface
{
	[Range(10, 200)]
	public int TestInt { get; set; } = 100;
	public string? TestString { get; set; }

	[EmailAddress]
	public string? EmailAddress { get; set; }

	[EmailAddress]
	[Compare(nameof(EmailAddress), ErrorMessage = "Emails must match")]
	public string? ConfirmEmailAddress { get; set; }

	public DateTime CreatedDate { get; set; }

	public double CurrentSalary { get; set; }

	[MaxPercentageOf(nameof(CurrentSalary), 0.2, ErrorMessage = "Please enter a value no more than 20% of the current salary.")]
	public double LumpSumOfCurrentSalary { get; set; }

	public double? CurrentSalaryNullable { get; set; }

	[MaxPercentageOf(nameof(CurrentSalaryNullable), 0.2, ErrorMessage = "Please enter a value no more than 20% of the current salary.")]
	public double? LumpSumOfCurrentSalaryNullable { get; set; }
}

[TypeScriptModel(TypeScriptOutputModelType.Class | TypeScriptOutputModelType.KnockoutClass)]
public class TestChildClass
{
	public int? TestChildInt { get; set; }
	public TestEnum TestChildEnum { get; set; }

	[TypeScriptIgnore]
	public int IgnoreMe { get; set; }

	[TypeScriptNull]
	[Required]
	public string IHaveADefaultButAmForcedToBeNull { get; set; } = "I should not be output!";

	[TypeScriptEmpty]
	public List<string>? TestStringList { get; set; }

	[TypeScriptOverride(typeof(string))]
	public List<int> TestIntList { get; set; } = [];

	public bool NeedAddress { get; set; }

	[RequiredIfFalse(nameof(NeedAddress))]
	public string? NeededIfFalse { get; set; }

	[RequiredIfTrue(nameof(NeedAddress))]
	public string? NeededIfTrue { get; set; }

	[RequiredIf(nameof(TestChildEnum), TestEnum.Value1, ErrorMessage = "Please enter the value")]
	public string? NeededIfEqualToEnum { get; set; }
}

[TypeScriptModel(TypeScriptOutputModelType.Interface | TypeScriptOutputModelType.KnockoutInterface)]
public interface ITestInterface
{
	DateTime CreatedDate { get; }
}

[TypeScriptModel(TypeScriptOutputModelType.Interface | TypeScriptOutputModelType.KnockoutClass)]
public class ConcreteTestClass : GenericTestClass<TestEnum>
{
}

[TypeScriptModel(TypeScriptOutputModelType.Interface | TypeScriptOutputModelType.KnockoutInterface)]
public interface IGenericTestInterface<TEnum>
	where TEnum : struct, Enum
{
	TEnum GenericEnum { get; set; }
}

public class GenericTestClass<TEnum> : IGenericTestInterface<TEnum>
	where TEnum : struct, Enum
{
	public TEnum GenericEnum { get; set; }
}