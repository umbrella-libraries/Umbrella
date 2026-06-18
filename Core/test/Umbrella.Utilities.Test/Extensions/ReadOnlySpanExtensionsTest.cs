using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Test.Extensions;

public class ReadOnlySpanExtensionsTest
{
	[Fact]
	public void Char_ToLower_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('A');

		ReadOnlySpan<char> readOnlySpan = span;
		Span<char> destination = stackalloc char[readOnlySpan.Length];

		readOnlySpan.ToLowerSlim(destination);

		Assert.Equal("aaaaaaaaaa", destination.ToString());
	}

	[Fact]
	public void Char_ToLowerInvariant_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('A');

		ReadOnlySpan<char> readOnlySpan = span;
		Span<char> destination = stackalloc char[readOnlySpan.Length];

		readOnlySpan.ToLowerInvariantSlim(destination);

		Assert.Equal("aaaaaaaaaa", destination.ToString());
	}

	[Fact]
	public void Char_ToUpper_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('a');

		ReadOnlySpan<char> readOnlySpan = span;
		Span<char> destination = stackalloc char[readOnlySpan.Length];

		readOnlySpan.ToUpperSlim(destination);

		Assert.Equal("AAAAAAAAAA", destination.ToString());
	}

	[Fact]
	public void Char_ToUpperInvariant_Valid()
	{
		Span<char> span = stackalloc char[10];
		span.Fill('a');

		ReadOnlySpan<char> readOnlySpan = span;
		Span<char> destination = stackalloc char[readOnlySpan.Length];

		readOnlySpan.ToUpperInvariantSlim(destination);

		Assert.Equal("AAAAAAAAAA", destination.ToString());
	}
}