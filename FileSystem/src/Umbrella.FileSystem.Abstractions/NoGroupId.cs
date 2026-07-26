namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A sentinel type used by <see cref="UmbrellaFileHandler"/> to represent the absence of a group identifier.
/// </summary>
public readonly struct NoGroupId : IEquatable<NoGroupId>
{
	/// <inheritdoc />
	public bool Equals(NoGroupId other) => true;

	/// <inheritdoc />
	public override bool Equals(object? obj) => obj is NoGroupId;

	/// <inheritdoc />
	public override int GetHashCode() => 0;

	/// <inheritdoc />
	public override string ToString() => string.Empty;

	/// <summary>Returns <see langword="true"/>; all <see cref="NoGroupId"/> instances are equal.</summary>
	public static bool operator ==(NoGroupId left, NoGroupId right) => left.Equals(right);

	/// <summary>Returns <see langword="false"/>; all <see cref="NoGroupId"/> instances are equal.</summary>
	public static bool operator !=(NoGroupId left, NoGroupId right) => !left.Equals(right);
}
