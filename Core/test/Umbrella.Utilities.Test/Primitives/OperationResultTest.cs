using Umbrella.Utilities.Primitives;

namespace Umbrella.Utilities.Test.Primitives;

public class OperationResultTest
{
	[Fact]
	public void ConcurrencyConflict_CreatesConcurrencyConflictStatus()
	{
		var result = OperationResult.ConcurrencyConflict("Changed elsewhere.");

		Assert.Equal(OperationResultStatus.ConcurrencyConflict, result.Status);
		Assert.False(result.IsSuccess);
		Assert.Equal("Changed elsewhere.", result.PrimaryValidationMessage);
	}

	[Fact]
	public void ConcurrencyConflict_Typed_CreatesConcurrencyConflictStatus()
	{
		var result = OperationResult<object>.ConcurrencyConflict("Changed elsewhere.");

		Assert.Equal(OperationResultStatus.ConcurrencyConflict, result.Status);
		Assert.False(result.IsSuccess);
		Assert.Equal("Changed elsewhere.", result.PrimaryValidationMessage);
	}

	[Fact]
	public void ToTypedOperationResult_WhenConcurrencyConflict_PreservesStatus()
	{
		var result = OperationResult.ConcurrencyConflict("Changed elsewhere.").ToTypedOperationResult<object>();

		Assert.Equal(OperationResultStatus.ConcurrencyConflict, result.Status);
		Assert.Equal("Changed elsewhere.", result.PrimaryValidationMessage);
	}
}
