using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.FriendlyUrl;

namespace Umbrella.Utilities.Test.FriendlyUrl;

public class FriendlyUrlTest
{
	[Fact]
	public void TestOnlySpecialCharacters()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("!\"£$%^&*()");

		Assert.Equal(string.Empty, url);
	}

	[Fact]
	public void TestSpecialCharactersWith3ValidCharsAtStartMiddleAndEnd()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("1!\"£$%f^&*()2");

		Assert.Equal("1-f-2", url);
	}

	[Fact]
	public void TestSpecialCharactersWith1ValidCharAtMiddle()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("!\"£$%f^&*()");

		Assert.Equal("f", url);
	}

	[Fact]
	public void NullInput_ReturnsEmptyString()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl(null!);
		Assert.Equal(string.Empty, url);
	}

	[Fact]
	public void MixedCaseSpacesAndPunctuation_Normalized()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("  Hello, World! This is A TEST  ");
		Assert.Equal("hello-world-this-is-a-test", url);
	}

	[Fact]
	public void ConsecutiveSeparators_CollapsedToSingleHyphen()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("Hello___World--Test!!!");
		Assert.Equal("hello-world-test", url);
	}

	[Fact]
	public void AccentedCharacters_RemappedToAscii()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("Café déjà-vu – São Paulo");
		Assert.Equal("cafe-deja-vu-sao-paulo", url);
	}

	[Fact]
	public void NonLatinCharacters_RemappedOrRemoved()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("Über 東京 2024!");
		Assert.Equal("uber-2024", url);
	}

	[Fact]
	public void MaxLength_TruncatesResult()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("This is a somewhat long title", maxLength: 10);
		Assert.Equal("this-is-a", url);
	}

	[Fact]
	public void MaxLength_ExactLength_NoExtraTrim()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("abc def", maxLength: 7);
		Assert.Equal("abc-def", url);
	}

	[Fact]
	public void LeadingAndTrailingSeparators_Trimmed()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("---Hello World---");
		Assert.Equal("hello-world", url);
	}

	[Fact]
	public void Filename_PeriodsConvertedToHyphen_CurrentBehavior()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("My Document.File.Name.txt");
		Assert.Equal("my-document-file-name-txt", url);
	}

	[Fact]
	public void MaxLength_TruncationRemovesTrailingHyphen()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("Long   Title -- With *** Multiple Parts", maxLength: 15);
		Assert.Equal("long-title-with", url);
	}

	// Ampersand-specific scenarios
	[Fact]
	public void Ampersand_SingleWithinPhrase_CollapsesWithSpaces()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("Research & Development");
		Assert.Equal("research-development", url);
	}

	[Fact]
	public void Ampersand_NoSpaces_ATAndT()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("AT&T");
		Assert.Equal("at-t", url);
	}

	[Fact]
	public void MultipleAmpersands_SequenceCollapses()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("Rock && Roll &&& Blues");
		Assert.Equal("rock-roll-blues", url);
	}

	[Fact]
	public void LeadingTrailingAmpersands_Trimmed()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("&&Great & Stuff&&");
		Assert.Equal("great-stuff", url);
	}

	[Fact]
	public void AmpersandWithMaxLength()
	{
		var generator = CreateFriendlyUrlGenerator();
		string url = generator.GenerateUrl("R&D Tools and Tips", maxLength: 12); // full slug would be r-d-tools-and-tips
		Assert.Equal("r-d-tools-an", url); // 12 chars, truncation mid-word
	}

	private static FriendlyUrlGenerator CreateFriendlyUrlGenerator()
	{
		var loggerMock = new Mock<ILogger<FriendlyUrlGenerator>>();
		return new FriendlyUrlGenerator(loggerMock.Object);
	}
}