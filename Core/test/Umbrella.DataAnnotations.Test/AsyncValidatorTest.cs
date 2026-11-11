using System.ComponentModel.DataAnnotations;
#if NET11_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
using Umbrella.DataAnnotations.Helpers;

namespace Umbrella.DataAnnotations.Test;

public sealed class AsyncValidatorTest
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Existing_Umbrella_async_attributes_are_awaited_once(bool valid)
	{
		var item = new AttributeItem { Value = valid ? "Valid" : "Invalid" };
		var context = new ValidationContext(item) { MemberName = nameof(AttributeItem.Value) };
		var results = new List<ValidationResult>();

		bool actual = await AsyncValidator.TryValidateValueAsync(item.Value, context, results, [new UmbrellaCheckAttribute()], TestContext.Current.CancellationToken);

		Assert.Equal(valid, actual);
		Assert.Equal(valid ? 0 : 1, results.Count);
		Assert.Equal(1, item.UmbrellaCalls);
	}

#if NET11_0_OR_GREATER
	[Theory]
	[InlineData(0, true)]
	[InlineData(0, false)]
	[InlineData(1, true)]
	[InlineData(1, false)]
	[InlineData(2, true)]
	[InlineData(2, false)]
	public async Task Native_and_Umbrella_async_attributes_work_together_for_objects_properties_and_values(int mode, bool valid)
	{
		var item = new AttributeItem { Value = valid ? "Valid" : "Invalid" };
		var context = new ValidationContext(item);
		var results = new List<ValidationResult>();
		CancellationToken token = TestContext.Current.CancellationToken;

		if (mode != 0)
			context.MemberName = nameof(AttributeItem.Value);

		bool actual = mode switch
		{
			0 => await AsyncValidator.TryValidateObjectAsync(item, context, results, true, token),
			1 => await AsyncValidator.TryValidatePropertyAsync(item.Value, context, results, token),
			_ => await AsyncValidator.TryValidateValueAsync(item.Value, context, results, [new NativeCheckAttribute(), new UmbrellaCheckAttribute()], token)
		};

		Assert.Equal(valid, actual);
		Assert.Equal(valid ? 0 : 2, results.Count);
		Assert.Equal(1, item.NativeCalls);
		Assert.Equal(1, item.UmbrellaCalls);
		Assert.Equal(token, item.NativeToken);
	}

	[Fact]
	public async Task Required_failure_prevents_native_async_validation()
	{
		var item = new AttributeItem();
		var results = new List<ValidationResult>();

		bool valid = await AsyncValidator.TryValidateObjectAsync(item, new ValidationContext(item), results, true, TestContext.Current.CancellationToken);

		Assert.False(valid);
		_ = Assert.Single(results);
		Assert.Equal(0, item.NativeCalls);
		Assert.Equal(0, item.UmbrellaCalls);
	}

	[Theory]
	[InlineData(true, 2)]
	[InlineData(false, 1)]
	public async Task Native_async_objects_receive_cancellation_and_stop_when_no_result_collection_is_supplied(bool collectResults, int expectedResults)
	{
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		var item = new NativeAsyncItem();
		List<ValidationResult>? results = collectResults ? [] : null;

		bool valid = await AsyncValidator.TryValidateObjectAsync(item, new ValidationContext(item), results, true, cancellation.Token);

		Assert.False(valid);
		Assert.Equal(expectedResults, item.ResultsProduced);
		Assert.Equal(cancellation.Token, item.Token);
		if (collectResults)
			Assert.Equal(expectedResults, results!.Count);
	}

	private sealed class NativeAsyncItem : IAsyncValidatableObject
	{
		internal int ResultsProduced { get; private set; }
		internal CancellationToken Token { get; private set; }

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
			=> throw new InvalidOperationException("The synchronous fallback must not run.");

		public async IAsyncEnumerable<ValidationResult> ValidateAsync(ValidationContext validationContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await Task.Yield();
			Token = cancellationToken;
			for (int index = 0; index < 2; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ResultsProduced++;
				yield return new ValidationResult("Native object validation failed.");
			}
		}
	}

	private sealed class NativeCheckAttribute : System.ComponentModel.DataAnnotations.AsyncValidationAttribute
	{
		protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
			=> throw new InvalidOperationException("The synchronous fallback must not run.");

		protected override async Task<ValidationResult?> IsValidAsync(object? value, ValidationContext validationContext, CancellationToken cancellationToken)
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			var item = (AttributeItem)validationContext.ObjectInstance;
			item.NativeCalls++;
			item.NativeToken = cancellationToken;
			return Equals(value, "Valid") ? ValidationResult.Success : new ValidationResult("Native attribute failed.");
		}
	}
#endif

	private sealed class AttributeItem
	{
		[Required]
		[UmbrellaCheck]
#if NET11_0_OR_GREATER
		[NativeCheck]
#endif
		public string? Value { get; init; }
		internal int UmbrellaCalls { get; set; }
#if NET11_0_OR_GREATER
		internal int NativeCalls { get; set; }
		internal CancellationToken NativeToken { get; set; }
#endif
	}

	private sealed class UmbrellaCheckAttribute : Umbrella.DataAnnotations.AsyncValidationAttribute
	{
		protected override async Task<ValidationResult?> IsValidAsync(object? value, ValidationContext validationContext, CancellationToken cancellationToken)
		{
			await Task.Yield();
			cancellationToken.ThrowIfCancellationRequested();
			((AttributeItem)validationContext.ObjectInstance).UmbrellaCalls++;
			return Equals(value, "Valid") ? ValidationResult.Success : new ValidationResult("Umbrella attribute failed.");
		}
	}
}
