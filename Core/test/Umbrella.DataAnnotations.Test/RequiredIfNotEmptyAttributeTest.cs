
namespace Umbrella.DataAnnotations.Test;

public class RequiredIfNotEmptyAttributeTest
{
	private class Model : ContingentValidationModelBase<RequiredIfNotEmptyAttribute>
	{
		public string? Value1 { get; set; }

		[RequiredIfNotEmpty("Value1")]
		public string? Value2 { get; set; }
	}

	[Fact]
	public void IsValidTest()
	{
		var model = new Model() { Value1 = "hello", Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotRequiredTest()
	{
		var model = new Model() { Value1 = "", Value2 = "" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotRequiredWithValue1NullTest()
	{
		var model = new Model() { Value1 = null, Value2 = null };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidTest()
	{
		var model = new Model() { Value1 = "hello", Value2 = "" };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidWithvalue2NullTest()
	{
		var model = new Model() { Value1 = "hello", Value2 = null };
		Assert.False(model.IsValid("Value2"));
	}
}