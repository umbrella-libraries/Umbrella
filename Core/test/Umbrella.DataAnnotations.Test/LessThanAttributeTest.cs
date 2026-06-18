
namespace Umbrella.DataAnnotations.Test;

public class LessThanAttributeTest
{
	private class DateModel : ContingentValidationModelBase<LessThanAttribute>
	{
		public DateTime? Value1 { get; set; }

		[LessThan("Value1")]
		public DateTime? Value2 { get; set; }
	}

	private class DateModelWithPassOnNull : ContingentValidationModelBase<LessThanAttribute>
	{
		public DateTime? Value1 { get; set; }

		[LessThan("Value1", PassOnNull = true)]
		public DateTime? Value2 { get; set; }
	}

	private class Int16Model : ContingentValidationModelBase<LessThanAttribute>
	{
		public short Value1 { get; set; }

		[LessThan("Value1")]
		public short Value2 { get; set; }
	}

	[Fact]
	public void DateIsValid()
	{
		var model = new DateModel() { Value1 = DateTime.Now, Value2 = DateTime.Now.AddDays(-1) };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void DateIsNotValid()
	{
		var model = new DateModel() { Value1 = DateTime.Now, Value2 = DateTime.Now.AddDays(1) };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void DateWithNullsIsNotValid()
	{
		var model = new DateModel() { };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void DateWithValue1NullIsNotValid()
	{
		var model = new DateModel() { Value2 = DateTime.Now };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void DateWithValue2NullIsNotValid()
	{
		var model = new DateModel() { Value1 = DateTime.Now };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void DateWithValue1NullIsValid()
	{
		var model = new DateModelWithPassOnNull() { Value2 = DateTime.Now };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void DateWithValue2NullIsValid()
	{
		var model = new DateModelWithPassOnNull() { Value1 = DateTime.Now };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void Int16IsValid()
	{
		var model = new Int16Model() { Value1 = 120, Value2 = 12 };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void Int16IsNotValid()
	{
		var model = new Int16Model() { Value1 = 12, Value2 = 120 };
		Assert.False(model.IsValid("Value2"));
	}
}