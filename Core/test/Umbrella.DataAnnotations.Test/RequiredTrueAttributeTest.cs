
namespace Umbrella.DataAnnotations.Test;

public class RequiredTrueAttributeTest
{
	private class Model : ValidationModelBase<RequiredTrueAttribute>
	{
		[RequiredTrue]
		public bool? Value1 { get; set; }
	}

	[Fact]
	public void IsValidTest()
	{
		var model = new Model() { Value1 = true };
		Assert.True(model.IsValid("Value1"));
	}

	// TODO: UNIT TEST FIXES
	//[Fact]
	//public void IsNotValidTest()
	//{
	//	var model = new Model() { Value1 = false };
	//	Assert.False(model.IsValid("Value1"));
	//}

	//[Fact]
	//public void IsNotValidNullTest()
	//{
	//	var model = new Model() { Value1 = null };
	//	Assert.False(model.IsValid("Value1"));
	//}
}