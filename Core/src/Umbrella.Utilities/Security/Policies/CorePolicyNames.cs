
namespace Umbrella.Utilities.Security.Policies;

/// <summary>
/// Specifies the names for core authorization policies that can be applied to resources.
/// </summary>
public static class CorePolicyNames
{
	/// <summary>
	/// The Create policy.
	/// </summary>
	public const string Create = nameof(Create);

	/// <summary>
	/// The Read policy.
	/// </summary>
	public const string Read = nameof(Read);

	/// <summary>
	/// The Update policy.
	/// </summary>
	public const string Update = nameof(Update);

	/// <summary>
	/// The Delete policy.
	/// </summary>
	public const string Delete = nameof(Delete);
}