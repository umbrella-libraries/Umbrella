
namespace Umbrella.DataAnnotations.Test;

public class RequiredIfNotRegExMatchAttributeTest
{
	private class Model : ContingentValidationModelBase<RequiredIfNotRegExMatchAttribute>
	{
		public string? Value1 { get; set; }

		[RequiredIfNotRegExMatch("Value1", "^ *(1[0-2]|0?[1-9]):[0-5][0-9] *(a|p|A|P)(m|M) *$")]
		public string? Value2 { get; set; }
	}

	[Fact]
	public void IsValidTest()
	{
		var model = new Model() { Value1 = "not a time", Value2 = "hello" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidTest()
	{
		var model = new Model() { Value1 = "not a time", Value2 = "" };
		Assert.False(model.IsValid("Value2"));
	}
}