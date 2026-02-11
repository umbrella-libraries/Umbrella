
namespace Umbrella.Utilities.Data.Concurrency;

/// <summary>
/// Adds supports for storing a concurrency token on an item.
/// </summary>
public interface IConcurrencyStamp
{
	/// <summary>
	/// Gets or sets the concurrency stamp.
	/// </summary>
	string ConcurrencyStamp { get; set; }
}