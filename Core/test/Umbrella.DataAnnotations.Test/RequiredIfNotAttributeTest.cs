
namespace Umbrella.DataAnnotations.Test;

public class RequiredIfNotAttributeTest
{
	private sealed class Model : ContingentValidationModelBase<RequiredIfNotAttribute>
	{
		public string? Value1 { get; set; }

		[RequiredIfNot("Value1", "hello")]
		public string? Value2 { get; set; }
	}

	[Fact]
	public void IsValidTest()
	{
		var model = new Model() { Value1 = "goodbye", Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidTest()
	{
		var model = new Model() { Value1 = "goodbye", Value2 = "" };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidWithValue2NullTest()
	{
		var model = new Model() { Value1 = "goodbye", Value2 = null };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotRequiredTest()
	{
		var model = new Model() { Value1 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsRequiredWithValue1NullTest()
	{
		var model = new Model() { Value1 = null };
		Assert.False(model.IsValid("Value2"));
	}
}