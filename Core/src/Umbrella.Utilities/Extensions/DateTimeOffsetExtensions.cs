namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for the <see cref="DateTimeOffset"/> type.
/// </summary>
public static class DateTimeOffsetExtensions
{
	extension(DateTimeOffset)
	{
		/// <summary>
		/// Returns the earlier of two specified DateTimeOffset values.
		/// </summary>
		/// <param name="a">The first DateTimeOffset value to compare.</param>
		/// <param name="b">The second DateTimeOffset value to compare.</param>
		/// <returns>The earlier of the two DateTimeOffset values. If both values are equal, the first value is returned.</returns>
		public static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;

		/// <summary>
		/// Returns the later of two specified DateTimeOffset values.
		/// </summary>
		/// <param name="a">The first DateTimeOffset value to compare.</param>
		/// <param name="b">The second DateTimeOffset value to compare.</param>
		/// <returns>The later of the two DateTimeOffset values. If both values are equal, the first value is returned.</returns>
		public static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
	}
}