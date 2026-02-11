


using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;

namespace Umbrella.Utilities.Encryption.Options;

/// <summary>
/// Options for the <see cref="SecureRandomStringGenerator"/> class.
/// </summary>
public class SecureRandomStringGeneratorOptions : IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the special characters to choose from. The default characters are: !@#$&amp;%
	/// </summary>
	/// <value>
	/// The special characters.
	/// </value>
	public IReadOnlyList<char> SpecialCharacters { get; set; } = new[] { '!', '@', '#', '$', '&', '%' };

	// TODO: Enable certain character exclusions which might cause confusion with letter, e.g. o, z, etc.

	/// <summary>
	/// Validates this instance.
	/// </summary>
	public void Validate() => Guard.IsNotNull(SpecialCharacters, nameof(SpecialCharacters));
}