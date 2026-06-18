
namespace Umbrella.DataAnnotations.Test;

public class EqualToAttributeTest
{
	private class Model : ContingentValidationModelBase<EqualToAttribute>
	{
		public string? Value1 { get; set; }

		[EqualTo("Value1")]
		public string? Value2 { get; set; }
	}

	private class ModelWithPassOnNull : ContingentValidationModelBase<EqualToAttribute>
	{
		public string? Value1 { get; set; }

		[EqualTo("Value1", PassOnNull = true)]
		public string? Value2 { get; set; }
	}

	[Fact]
	public void IsValid()
	{
		var model = new Model() { Value1 = "hello", Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValid()
	{
		var model = new Model() { Value1 = "hello", Value2 = "goodbye" };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsValidWithNulls()
	{
		var model = new Model() { };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidWithValue1Null()
	{
		var model = new Model() { Value2 = "hello" };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidWithValue2Null()
	{
		var model = new Model() { Value1 = "hello" };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void WhenPassOnNull_IsValidWithValue1Null()
	{
		var model = new ModelWithPassOnNull() { Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void WhenPassOnNull_IsValidWithValue2Null()
	{
		var model = new ModelWithPassOnNull() { Value1 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void WhenPassOnNull_IsValidWithBothNull()
	{
		var model = new ModelWithPassOnNull();
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void WhenPassOnNull_IsValidWithEqual()
	{
		var model = new ModelWithPassOnNull { Value1 = "hello", Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void WhenPassOnNull_IsNotValidWithWhenNotEqual()
	{
		var model = new ModelWithPassOnNull { Value1 = "hello1", Value2 = "hello" };
		Assert.False(model.IsValid("Value2"));
	}
}