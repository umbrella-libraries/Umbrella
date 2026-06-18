
namespace Umbrella.DataAnnotations.Test;

public class RequiredNonEmptyCollectionIfAttributeTest
{
	private class Model : ContingentValidationModelBase<RequiredNonEmptyCollectionIfAttribute>
	{
		public string? Value1 { get; set; }

		[RequiredNonEmptyCollectionIf("Value1", "hello")]
		public List<string>? Value2 { get; set; }
	}

	private class ComplexModel : ContingentValidationModelBase<RequiredIfAttribute>
	{
		public class SubModel
		{
			public string? InnerValue { get; set; }
		}

		public SubModel? Value1 { get; set; }

		[RequiredNonEmptyCollectionIf("Value1.InnerValue", "hello")]
		public List<string>? Value2 { get; set; }
	}

	[Fact]
	public void IsValidTest()
	{
		var model = new Model() { Value1 = "hello", Value2 = ["hello"] };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsValidTestComplex()
	{
		var model = new ComplexModel() { Value1 = new ComplexModel.SubModel() { InnerValue = "hello" }, Value2 = ["bla"] };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidTest()
	{
		var model = new Model() { Value1 = "hello", Value2 = null };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidTestComplex()
	{
		var model = new ComplexModel() { Value1 = new ComplexModel.SubModel() { InnerValue = "hello" }, Value2 = null };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotValidWithValue2NullTest()
	{
		var model = new Model() { Value1 = "hello", Value2 = null };
		Assert.False(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotRequiredTest()
	{
		var model = new Model() { Value1 = "goodbye" };
		Assert.True(model.IsValid("Value2"));
	}

	[Fact]
	public void IsNotRequiredWithValue1NullTest()
	{
		var model = new Model() { Value1 = null };
		Assert.True(model.IsValid("Value2"));
	}
}