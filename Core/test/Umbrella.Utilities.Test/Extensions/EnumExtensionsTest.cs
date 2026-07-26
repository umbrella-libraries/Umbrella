using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Test.Extensions;

public class EnumExtensionsTest
{
	[Flags]
	public enum TestType
	{
		None = 0,
		Type1 = 0b0001,
		Type2 = 0b0010,
		Type3 = 0b0100,
		Type4 = 0b1000,
		All = 0b1111
	}

	public static object[][] ToFlagsStringTestData =
	[
		[() => (TestType.Type1 | TestType.Type2 | TestType.Type4).ToFlagsString(), "Type1,Type2,Type4"],
		[() => (TestType.Type1 | TestType.Type4).ToFlagsString(), "Type1,Type4"],
		[() => (TestType.Type1 | TestType.Type4).ToFlagsString(x => x.ToUpperInvariant()), "TYPE1,TYPE4"],
		[() => (TestType.Type1 | TestType.Type4).ToFlagsString(separator: "+"), "Type1+Type4"],
	];

	public static object[][] ToFlagsDisplayStringTestData =
	[
		[() => (TestType.Type1 | TestType.Type2 | TestType.Type4).ToFlagsDisplayString(), "Type 1,Type 2,Type 4"],
		[() => (TestType.Type1 | TestType.Type4).ToFlagsDisplayString(), "Type 1,Type 4"],
		[() => (TestType.Type1 | TestType.Type4).ToFlagsDisplayString(separator: " + "), "Type 1 + Type 4"],
	];

	[Fact]
	public void ToFlagsString_NullSeparator() => Assert.Throws<ArgumentNullException>(() => TestType.Type1.ToFlagsString(separator: null!));

	[Theory]
	[MemberData(nameof(ToFlagsStringTestData))]
	public void ToFlagsString_Valid(Func<string> func, string expectedOutput)
	{
		ArgumentNullException.ThrowIfNull(func);

		string result = func();

		Assert.Equal(expectedOutput, result);
	}

	[Theory]
	[MemberData(nameof(ToFlagsDisplayStringTestData))]
	public void ToFlagsDisplayString_Valid(Func<string> func, string expectedOutput)
	{
		ArgumentNullException.ThrowIfNull(func);

		string result = func();

		Assert.Equal(expectedOutput, result);
	}

	[Fact]
	public void ToFlagsDisplayString_NullSeparator() => Assert.Throws<ArgumentNullException>(() => TestType.Type1.ToFlagsDisplayString(separator: null!));
}
