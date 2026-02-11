


using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.Numerics;
using Xunit;

namespace Umbrella.Utilities.Test.Numerics;

public class ConcurrentRandomGeneratorTest
{
	private readonly ConcurrentRandomGenerator _concurrentRandomGenerator;

	public ConcurrentRandomGeneratorTest()
	{
		_concurrentRandomGenerator = CreateConcurrentRandomGenerator();
	}

	[Fact]
	public void Next_Valid()
	{
		int num = _concurrentRandomGenerator.GetNext();

		Assert.True(num > -1);
	}

	[Fact]
	public void Next_InvalidMin()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GetNext(-1));

	[Fact]
	public void Next_InvalidMax()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GetNext(0, -1));

	[Fact]
	public void Next_InvalidMinMax()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GetNext(4, 3));

	[Theory]
	[InlineData(0, 0)]
	[InlineData(10, 10)]
	public void Next_EqMinMax_Valid(int min, int max)
	{
		int num = _concurrentRandomGenerator.GetNext(min, max);

		Assert.Equal(min, num);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(0, 2)]
	[InlineData(1, 2)]
	[InlineData(1, 3)]
	[InlineData(0, 99)]
	[InlineData(100, 300)]
	public void Next_NEqMinMax_Valid(int min, int max)
	{
		int num = _concurrentRandomGenerator.GetNext(min, max);

		Assert.True(num >= min && num < max);
	}

	[Fact]
	public void GenerateDistinctList_InvalidMin()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GenerateDistinctCollection(-1, 0, 1));

	[Fact]
	public void GenerateDistinctList_InvalidMax()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GenerateDistinctCollection(0, -1, 1));

	[Fact]
	public void GenerateDistinctList_InvalidMinMax()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GenerateDistinctCollection(4, 3, 1));

	[Theory]
	[InlineData(0, 1, 0)]
	[InlineData(9, 10, 2)]
	[InlineData(10, 12, 3)]
	public void GenerateDistinctList_InvalidCount(int min, int max, int count)
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.GenerateDistinctCollection(min, max, count));

	[Theory]
	[InlineData(0, 0, 1)]
	[InlineData(10, 10, 1)]
	public void GenerateDistinctList_EqMinMaxCount_Valid(int min, int max, int count)
	{
		var items = _concurrentRandomGenerator.GenerateDistinctCollection(min, max, count);

		Assert.Equal(count, items.Count);
		Assert.Equal(count, items.Distinct().Count());
	}

	[Theory]
	[InlineData(0, 9, 1, false)]
	[InlineData(0, 4, 2, false)]
	[InlineData(1, 5, 1, false)]
	[InlineData(1, 10, 2, false)]
	[InlineData(0, 99, 10, false)]
	[InlineData(100, 300, 27, false)]
	[InlineData(0, 9, 1, true)]
	[InlineData(0, 4, 2, true)]
	[InlineData(1, 5, 1, true)]
	[InlineData(1, 10, 2, true)]
	[InlineData(0, 99, 10, true)]
	[InlineData(100, 300, 27, true)]
	public void GenerateDistinctList_NEqMinMaxCount_Valid(int min, int max, int count, bool shuffle)
	{
		var items = _concurrentRandomGenerator.GenerateDistinctCollection(min, max, count, shuffle);

		Assert.Equal(count, items.Count);
		Assert.Equal(count, items.Distinct().Count());
	}

	[Theory]
	[InlineData(0, 1, 1, false)]
	[InlineData(0, 2, 2, false)]
	[InlineData(1, 2, 1, false)]
	[InlineData(1, 3, 2, false)]
	[InlineData(0, 99, 99, false)]
	[InlineData(100, 300, 200, false)]
	[InlineData(0, 1, 1, true)]
	[InlineData(0, 2, 2, true)]
	[InlineData(1, 2, 1, true)]
	[InlineData(1, 3, 2, true)]
	[InlineData(0, 99, 99, true)]
	[InlineData(100, 300, 200, true)]
	public void GenerateDistinctList_NEqMinMaxCountFullRange_Valid(int min, int max, int count, bool shuffle)
	{
		var items = _concurrentRandomGenerator.GenerateDistinctCollection(min, max, count, shuffle);

		Assert.Equal(count, items.Count);
		Assert.Equal(count, items.Distinct().Count());
	}

	[Fact]
	public void TakeRandom_Source_Null()
		=> Assert.Throws<ArgumentNullException>(() => _concurrentRandomGenerator.TakeRandom<object>(null!, 1).ToArray());

	[Fact]
	public void TakeRandom_Source_Empty()
	{
		string[] items = [];

		var result = _concurrentRandomGenerator.TakeRandom(items, 1);

		Assert.Empty(result);
	}

	[Theory]
	[InlineData(0)] // Zero not allowed
	[InlineData(3)] // Greater than the source size
	public void TakeRandom_Count_Invalid(int count)
		=> Assert.Throws<ArgumentOutOfRangeException>(() => _concurrentRandomGenerator.TakeRandom([1, 2], count).ToArray());

	[Theory]
	[InlineData(1, false)]
	[InlineData(2, false)]
	[InlineData(3, false)]
	[InlineData(4, false)]
	[InlineData(5, false)]
	[InlineData(6, false)]
	[InlineData(7, false)]
	[InlineData(8, false)]
	[InlineData(9, false)]
	[InlineData(10, false)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, true)]
	[InlineData(4, true)]
	[InlineData(5, true)]
	[InlineData(6, true)]
	[InlineData(7, true)]
	[InlineData(8, true)]
	[InlineData(9, true)]
	[InlineData(10, true)]
	public void TakeRandom_Valid(int count, bool shuffle)
	{
		int[] items = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

		int[] result = _concurrentRandomGenerator.TakeRandom(items, count, shuffle).ToArray();

		Assert.Equal(count, result.Length);
		Assert.Equal(count, result.Distinct().Count());
	}

	private static ConcurrentRandomGenerator CreateConcurrentRandomGenerator()
	{
		var loggerMock = new Mock<ILogger<ConcurrentRandomGenerator>>();

		return new ConcurrentRandomGenerator(loggerMock.Object);
	}
}