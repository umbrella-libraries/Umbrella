
namespace Umbrella.DataAnnotations.Test;

public class UmbrellaUrlAttributeTest
{
	private class ModelNoSchemaRequired : ValidationModelBase<UmbrellaUrlAttribute>
	{
		[UmbrellaUrl]
		public string? Value { get; set; }
	}

	private class ModelSchemaRequired : ValidationModelBase<UmbrellaUrlAttribute>
	{
		[UmbrellaUrl(true)]
		public string? Value { get; set; }
	}

	[Fact]
	public void IsValidNoSchemaTest()
	{
		var model = new ModelNoSchemaRequired() { Value = "www.google.com" };
		Assert.True(model.IsValid("Value"));
	}

	[Fact]
	public void IsValidHttpSchemaRequiredTest()
	{
		var model = new ModelSchemaRequired() { Value = "http://www.google.com" };
		Assert.True(model.IsValid("Value"));
	}

	[Fact]
	public void IsValidHttpsSchemaRequiredTest()
	{
		var model = new ModelSchemaRequired() { Value = "https://www.google.com" };
		Assert.True(model.IsValid("Value"));
	}

	[Fact]
	public void IsInvalidNoSchemaTest()
	{
		var model = new ModelSchemaRequired() { Value = "www.google.com" };
		Assert.True(!model.IsValid("Value"));
	}

	[Fact]
	public void IsValidHttpSchemaOptionalTest()
	{
		var model = new ModelNoSchemaRequired() { Value = "http://www.google.com" };
		Assert.True(model.IsValid("Value"));
	}

	[Fact]
	public void IsValidHttpsSchemaOptionalTest()
	{
		var model = new ModelNoSchemaRequired() { Value = "https://www.google.com" };
		Assert.True(model.IsValid("Value"));
	}
}