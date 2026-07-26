using System.Text.Json;
using Umbrella.Utilities.Data.Pagination;

namespace Umbrella.Utilities.Test.Data.Pagination;

public class PaginatedResultModelTest
{
	[Fact]
	public void DefaultConstructor_UsesEmptyDefaults()
	{
		var result = new PaginatedResultModel<int>();

		Assert.Empty(result.Items);
		Assert.Equal(0, result.PageNumber);
		Assert.Equal(0, result.PageSize);
		Assert.Equal(0, result.TotalCount);
		Assert.False(result.MoreItems);
	}

	[Theory]
	[InlineData(1, 2, 5, true)]
	[InlineData(1, 2, 2, false)]
	[InlineData(2, 2, 5, true)]
	[InlineData(3, 2, 5, false)]
	[InlineData(0, 2, 5, true)]
	public void Constructor_ComputesMoreItems(int pageNumber, int pageSize, int totalCount, bool expected)
	{
		int[] items = [1, 2];

		var result = new PaginatedResultModel<int>(items, pageNumber, pageSize, totalCount);

		Assert.Same(items, result.Items);
		Assert.Equal(pageNumber, result.PageNumber);
		Assert.Equal(pageSize, result.PageSize);
		Assert.Equal(totalCount, result.TotalCount);
		Assert.Equal(expected, result.MoreItems);
	}

	[Fact]
	public void WithExpression_RecomputesMoreItems()
	{
		int[] items = [1, 2];
		var firstPage = new PaginatedResultModel<int>
		{
			Items = items,
			PageNumber = 1,
			PageSize = 2,
			TotalCount = 5
		};

		PaginatedResultModel<int> lastPage = firstPage with { PageNumber = 3 };

		Assert.True(firstPage.MoreItems);
		Assert.False(lastPage.MoreItems);
		Assert.Same(items, lastPage.Items);
	}

	[Fact]
	public void JsonSerialization_RoundTripsPaginationData()
	{
		var expected = new PaginatedResultModel<int>([1, 2], 1, 2, 5);

		string json = JsonSerializer.Serialize(expected);
		var actual = JsonSerializer.Deserialize<PaginatedResultModel<int>>(json);

		Assert.NotNull(actual);
		Assert.Equal(expected.Items, actual.Items);
		Assert.Equal(expected.PageNumber, actual.PageNumber);
		Assert.Equal(expected.PageSize, actual.PageSize);
		Assert.Equal(expected.TotalCount, actual.TotalCount);
		Assert.Equal(expected.MoreItems, actual.MoreItems);
	}

	[Fact]
	public void DerivedRecord_SatisfiesNewConstraint()
	{
		DerivedPaginatedResultModel result = Create<DerivedPaginatedResultModel>();

		Assert.Empty(result.Items);
	}

	private static TResult Create<TResult>()
		where TResult : PaginatedResultModel<int>, new()
		=> new();

	private sealed record DerivedPaginatedResultModel : PaginatedResultModel<int>;
}
