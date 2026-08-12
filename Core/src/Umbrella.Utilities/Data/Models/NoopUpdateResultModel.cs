namespace Umbrella.Utilities.Data.Models;

/// <summary>
/// A no-op result model. This exists for use when specifying generic parameters for types that make use of the model types
/// but don't support updating items.
/// </summary>
/// <seealso cref="IUpdateResultModel" />
public class NoopUpdateResultModel : IUpdateResultModel
{
	/// <inheritdoc />
	public string ConcurrencyStamp => throw new NotImplementedException();
}