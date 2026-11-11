using System.ComponentModel.DataAnnotations;
#if NET11_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.Utilities.DataAnnotations;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.DataAnnotations.Enumerations;
#if NET11_0_OR_GREATER
using Umbrella.Utilities.DataAnnotations.Options;
#endif

namespace Umbrella.Utilities.Test.DataAnnotations;

public class UmbrellaValidatorTest
{
	[Theory]
	[InlineData(0, false)]
	[InlineData(5, true)]
	public async Task ValidateItemAsync_Shallow_ValidatesNonRequiredPropertiesAsync(int value, bool expectedValid)
	{
		using var services = new ServiceCollection().BuildServiceProvider();
		var validator = new UmbrellaValidator(
			NullLogger<UmbrellaValidator>.Instance,
			Mock.Of<IObjectGraphValidator>(),
			services);

		var result = await validator.ValidateItemAsync(new ValidatedItem { Value = value }, ValidationType.Shallow);

		Assert.Equal(expectedValid, result.isValid);
		Assert.Equal(expectedValid ? 0 : 1, result.results.Count);
	}

	private sealed class ValidatedItem
	{
		[Range(1, 10)]
		public int Value { get; init; }
	}

#if NET11_0_OR_GREATER
	[Theory]
	[InlineData(ValidationType.Shallow, true)]
	[InlineData(ValidationType.Shallow, false)]
	[InlineData(ValidationType.Deep, true)]
	[InlineData(ValidationType.Deep, false)]
	public async Task Native_async_object_validation_is_consistent_for_shallow_and_deep_validation(ValidationType validationType, bool valid)
	{
		using var services = new ServiceCollection().BuildServiceProvider();
		var validator = CreateValidator(services);
		var item = new NativeAsyncItem { IsValid = valid };

		var result = await validator.ValidateItemAsync(item, validationType);

		Assert.Equal(valid, result.isValid);
		Assert.Equal(valid ? 0 : 1, result.results.Count);
		Assert.Equal(1, item.ValidationCalls);
		if (!valid)
			Assert.Equal("Native object validation failed.", Assert.Single(result.results).ErrorMessage);
	}

	[Fact]
	public async Task Deep_validation_applies_native_async_attributes_to_nested_models()
	{
		using var services = new ServiceCollection().BuildServiceProvider();
		var validator = CreateValidator(services);
		var item = new ParentItem();

		var result = await validator.ValidateItemAsync(item, ValidationType.Deep);

		Assert.False(result.isValid);
		var error = Assert.IsType<ObjectGraphValidationResult>(Assert.Single(result.results));
		Assert.Same(item.Child, error.Model);
		Assert.Equal("Native attribute validation failed.", error.ErrorMessage);
		Assert.Equal([nameof(NativeAttributeItem.Value)], error.MemberNames);
	}

	private static UmbrellaValidator CreateValidator(IServiceProvider services)
		=> new(NullLogger<UmbrellaValidator>.Instance,
			new ObjectGraphValidator(NullLogger<ObjectGraphValidator>.Instance, new ObjectGraphValidatorOptions(), services), services);

	private sealed class NativeAsyncItem : IAsyncValidatableObject
	{
		public bool IsValid { get; init; }
		public int ValidationCalls { get; private set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
			=> throw new InvalidOperationException("The synchronous fallback must not run.");

		public async IAsyncEnumerable<ValidationResult> ValidateAsync(ValidationContext validationContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			ValidationCalls++;
			if (!IsValid)
				yield return new ValidationResult("Native object validation failed.");
		}
	}

	private sealed class ParentItem
	{
		public NativeAttributeItem Child { get; } = new();
	}

	private sealed class NativeAttributeItem
	{
		[NativeReject]
		public string Value { get; } = "Rejected";
	}

	private sealed class NativeRejectAttribute : System.ComponentModel.DataAnnotations.AsyncValidationAttribute
	{
		protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
			=> throw new InvalidOperationException("The synchronous fallback must not run.");

		protected override async Task<ValidationResult?> IsValidAsync(object? value, ValidationContext validationContext, CancellationToken cancellationToken)
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			return new ValidationResult("Native attribute validation failed.", [validationContext.MemberName!]);
		}
	}
#endif
}
