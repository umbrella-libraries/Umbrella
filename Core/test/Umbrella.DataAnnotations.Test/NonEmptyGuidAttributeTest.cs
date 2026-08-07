using System.ComponentModel.DataAnnotations;

namespace Umbrella.DataAnnotations.Test;

public class NonEmptyGuidAttributeTest
{
	private sealed class Model : ValidationModelBase<NonEmptyGuidAttribute>
	{
		[NonEmptyGuid]
		public Guid Value { get; set; }

		[NonEmptyGuid]
		public Guid? NullableValue { get; set; }
	}

	[Fact]
	public void IsValidTest()
	{
		var model = new Model { Value = Guid.NewGuid() };
		Assert.True(model.IsValid(nameof(Model.Value)));
	}

	[Fact]
	public void IsNotValidEmptyTest()
	{
		var model = new Model { Value = Guid.Empty };
		Assert.False(model.IsValid(nameof(Model.Value)));
	}

	[Fact]
	public void IsValidNullableTest()
	{
		var model = new Model { NullableValue = Guid.NewGuid() };
		Assert.True(model.IsValid(nameof(Model.NullableValue)));
	}

	[Fact]
	public void IsValidNullableNullTest()
	{
		var model = new Model { NullableValue = null };
		Assert.True(model.IsValid(nameof(Model.NullableValue)));
	}

	[Fact]
	public void IsNotValidNullableEmptyTest()
	{
		var model = new Model { NullableValue = Guid.Empty };
		Assert.False(model.IsValid(nameof(Model.NullableValue)));
	}

	[Fact]
	public void IsNotValidNonGuidValueTest()
	{
		var model = new Model();
		Assert.False(model.GetAttribute(nameof(Model.Value)).IsValid("not-a-guid"));
	}

	[Fact]
	public void ValidationResultContainsMemberNameTest()
	{
		var model = new Model { Value = Guid.Empty };
		NonEmptyGuidAttribute attribute = model.GetAttribute(nameof(Model.Value));
		var validationContext = new ValidationContext(model) { MemberName = nameof(Model.Value) };

		ValidationResult? result = attribute.GetValidationResult(model.Value, validationContext);

		Assert.NotNull(result);
		Assert.Equal(nameof(Model.Value), Assert.Single(result.MemberNames));
	}
}