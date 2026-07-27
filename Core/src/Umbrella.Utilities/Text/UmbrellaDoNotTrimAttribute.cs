namespace Umbrella.Utilities.Text;

/// <summary>
/// Prevents a property from being modified by <see cref="IUmbrellaTrimmable.TrimAllStringProperties"/>.
/// </summary>
/// <remarks>
/// Use this for values where leading or trailing whitespace is meaningful, such as passwords.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaDoNotTrimAttribute : Attribute
{
}
