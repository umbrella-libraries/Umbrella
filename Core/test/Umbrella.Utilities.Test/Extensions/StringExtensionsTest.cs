using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Test.Extensions;

public class StringExtensionsTest
{
	[Fact]
	public void RemapInternationalCharactersToAscii_Valid()
	{
		string input = "Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF)";

		string output = input.RemapInternationalCharactersToAscii();

		Assert.Equal("Rio Tinto Minera Peru Limitada SAC (RTMPL) Borax Francais (BF)", output);
	}
}