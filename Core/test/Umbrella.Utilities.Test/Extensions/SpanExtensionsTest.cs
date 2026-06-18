using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Test.Extensions;

public class SpanExtensionsTest
{
	[Fact]
	public void Char_AppendStringToEmpty_Valid()
	{
		Span<char> span = stackalloc char[10];

		int currentIndex = span.Write(0, "1234567890");

		Assert.Equal(10, currentIndex);
		Assert.Equal("1234567890", span.ToString());
	}

	[Fact]
	public void Char_AppendStringToExisting_Valid()
	{
		Span<char> span = stackalloc char[10];
		span[0] = '1';
		span[1] = '2';
		span[2] = '3';
		span[3] = '4';
		span[4] = '5';

		int currentIndex = span.Write(5, "67890");

		Assert.Equal(10, currentIndex);
		Assert.Equal("1234567890", span.ToString());
	}

	[Fact]
	public void Char_AppendReadOnlySpanToEmpty_Valid()
	{
		Span<char> span = stackalloc char[10];

		int currentIndex = span.Write(0, "1234567890".AsSpan());

		Assert.Equal(10, currentIndex);
		Assert.Equal("1234567890", span.ToString());
	}

	[Fact]
	public void Char_AppendReadOnlySpanToExisting_Valid()
	{
		Span<char> span = stackalloc char[10];
		span[0] = '1';
		span[1] = '2';
		span[2] = '3';
		span[3] = '4';
		span[4] = '5';

		int currentIndex = span.Write(5, "67890".AsSpan());

		Assert.Equal(10, currentIndex);
		Assert.Equal("1234567890", span.ToString());
	}

	[Fact]
	public void Char_ToLower_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('A');

		span.ToLower();

		Assert.Equal("aaaaaaaaaa", span.ToString());
	}

	[Fact]
	public void Char_ToLowerInvariant_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('A');

		span.ToLowerInvariant();

		Assert.Equal("aaaaaaaaaa", span.ToString());
	}

	[Fact]
	public void Char_ToUpper_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('a');

		span.ToUpper();

		Assert.Equal("AAAAAAAAAA", span.ToString());
	}

	[Fact]
	public void Char_ToUpperInvariant_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('a');

		span.ToUpperInvariant();

		Assert.Equal("AAAAAAAAAA", span.ToString());
	}
}