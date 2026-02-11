


using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extensions methods that operate on any <see cref="object"/>.
/// </summary>
public static class ObjectExtensions
{
	/// <summary>
	/// Converts to the specified <paramref name="value"/> to a JSON string.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="useCamelCasingRules">if set to <see langword="true"/>, uses camelcase naming rules for property names.</param>
	/// <returns>The JSON string.</returns>
	public static string ToJsonString(this object value, bool useCamelCasingRules = false)
	{
		Guard.IsNotNull(value);

		return UmbrellaStatics.SerializeJson(value, useCamelCasingRules);
	}
}