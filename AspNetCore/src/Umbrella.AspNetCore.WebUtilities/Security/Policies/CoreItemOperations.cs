


using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Umbrella.AspNetCore.WebUtilities.Security.Policies;

/// <summary>
/// Specifies authorization requirements for core operations that can be performed on resources.
/// </summary>
public static class CoreItemOperations
{
	/// <summary>
	/// The Create operation.
	/// </summary>
	public static OperationAuthorizationRequirement Create { get; } = new() { Name = nameof(Create) };

	/// <summary>
	/// The Read operation.
	/// </summary>
	public static OperationAuthorizationRequirement Read { get; } = new() { Name = nameof(Read) };

	/// <summary>
	/// The Update operation.
	/// </summary>
	public static OperationAuthorizationRequirement Update { get; } = new() { Name = nameof(Update) };

	/// <summary>
	/// The Delete operation.
	/// </summary>
	public static OperationAuthorizationRequirement Delete { get; } = new() { Name = nameof(Delete) };
}