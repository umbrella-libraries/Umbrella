using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Data;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;

namespace Umbrella.Utilities.Test.Data;

public class DataExpressionFactoryTest
{
	public class DataItem
	{
		public string Name { get; set; } = "Richard";
		public ChildDataItem Child { get; set; } = new ChildDataItem();
	}

	public class ChildDataItem
	{
		public int Count { get; set; } = 954;
		public PetDataItem Pet { get; set; } = new PetDataItem();
	}

	public class PetDataItem
	{
		public string Breed { get; set; } = "Shi Tzu";
	}

	[Theory]
	[InlineData("Name", SortDirection.Ascending, "Richard")]
	[InlineData("Child.Count", SortDirection.Descending, 954)]
	[InlineData("Child.Pet.Breed", SortDirection.Descending, "Shi Tzu")]
	public void SortDescriptor_Valid(string memberPath, SortDirection sortDirection, object result)
	{
		var factory = CreateDataExpressionFactory();
		var descriptor = new SortExpressionDescriptor(memberPath, sortDirection);

		IDataExpression? dataExpression = factory.Create(typeof(SortExpression<DataItem>), descriptor);

		Assert.NotNull(dataExpression);
		Assert.Equal(memberPath, dataExpression!.MemberPath);
		_ = Assert.IsType<SortExpression<DataItem>>(dataExpression);

		var sortExpression = (SortExpression<DataItem>)dataExpression;

		Assert.Equal(sortDirection, sortExpression.Direction);
		Assert.Equal(result, sortExpression.GetDelegate()?.Invoke(new DataItem()));
	}

	[Theory]
	[InlineData("Name", "Rich", FilterType.Contains, "Richard")]
	[InlineData("Child.Count", "1", FilterType.Equal, 954)]
	[InlineData("Child.Pet.Breed", "ddd", FilterType.Contains, "Shi Tzu")]
	public void FilterDescriptor_Valid(string memberPath, string value, FilterType filterType, object result)
	{
		var factory = CreateDataExpressionFactory();
		var descriptor = new FilterExpressionDescriptor(memberPath, value, filterType);

		IDataExpression? dataExpression = factory.Create(typeof(FilterExpression<DataItem>), descriptor);

		Assert.NotNull(dataExpression);
		Assert.Equal(memberPath, dataExpression!.MemberPath);
		_ = Assert.IsType<FilterExpression<DataItem>>(dataExpression);

		var filterExpression = (FilterExpression<DataItem>)dataExpression;

		Assert.Equal(value, filterExpression.Value);
		Assert.Equal(filterType, filterExpression.Type);
		Assert.Equal(result, filterExpression.GetDelegate()?.Invoke(new DataItem()));
	}

	private static DataExpressionFactory CreateDataExpressionFactory() => new(CoreUtilitiesMocks.CreateLogger<DataExpressionFactory>());
}