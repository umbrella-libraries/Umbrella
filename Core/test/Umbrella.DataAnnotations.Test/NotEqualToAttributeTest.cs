
namespace Umbrella.DataAnnotations.Test;

public class NotEqualToAttributeTest
{
	private sealed class Model : ContingentValidationModelBase<NotEqualToAttribute>
	{
		public string? Value1 { get; set; }

		[NotEqualTo("Value1")]
		public string? Value2 { get; set; }
	}

	[Fact]
	public void IsValid()
	{
		var model = new Model() { Value1 = "hello", Value2 = "goodbye" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValid()
	{
		var model = new Model() { Value1 = "hello", Value2 = "hello" };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidWithNulls()
	{
		var model = new Model() { };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsValidWithValue1Null()
	{
		var model = new Model() { Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsValidWithValue2Null()
	{
		var model = new Model() { Value1 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}
}