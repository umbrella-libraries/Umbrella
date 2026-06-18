using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Umbrella.Utilities.Comparers;

namespace Umbrella.DataAccess.Abstractions.Test;

// TODO: Add test for the other method and more data for the first test.
public class EntityValidatorTest
{
	private static readonly GenericEqualityComparer<ValidationResult?, string?> _validationResultComparer = new(x => x?.ErrorMessage, (x, y) => x is not null && y is not null && x.ErrorMessage == y.ErrorMessage && x.MemberNames.SequenceEqual(y.MemberNames));

	[Theory]
	[InlineData(null, "Prop1", 1, 2, true, false)]
	[InlineData("", "Prop1", 1, 2, true, false)]
	[InlineData("    ", "Prop1", 1, 2, true, false)]
	[InlineData(null, "Prop1", 1, 2, false, true)]
	[InlineData("", "Prop1", 1, 2, false, true)]
	[InlineData("    ", "Prop1", 1, 2, false, true)]
	public void ValidatePropertyStringLengthTest(string value, string propertyName, int minLength, int maxLength, bool required, bool shouldPass)
	{
		EntityValidator entityValidator = CreateEntityValidator();

		ValidationResult? result = entityValidator.ValidatePropertyStringLength(value, propertyName, minLength, maxLength, required);

		ValidationResult? expected = shouldPass
			? ValidationResult.Success!
			: new ValidationResult(string.Format(CultureInfo.InvariantCulture, ErrorMessages.InvalidPropertyStringLengthErrorMessageFormat, propertyName, minLength, maxLength), new[] { propertyName });

		Assert.Equal(expected, result, _validationResultComparer);
	}

	[Theory]
	[InlineData(null, "Prop1", 1, 2, true, false)]
	[InlineData(null, "Prop1", 1, 2, false, true)]
	public void ValidatePropertyNumberRange(int? value, string propertyName, int minLength, int maxLength, bool required, bool shouldPass)
	{
		EntityValidator entityValidator = CreateEntityValidator();

		ValidationResult? result = entityValidator.ValidatePropertyNumberRange(value, propertyName, minLength, maxLength, required);

		ValidationResult expected = shouldPass
			? ValidationResult.Success!
			: new ValidationResult(string.Format(CultureInfo.InvariantCulture, ErrorMessages.InvalidPropertyNumberRangeErrorMessageFormat, propertyName, minLength, maxLength), new[] { propertyName });

		Assert.Equal(expected, result, _validationResultComparer);
	}

	private static EntityValidator CreateEntityValidator() => new();
}