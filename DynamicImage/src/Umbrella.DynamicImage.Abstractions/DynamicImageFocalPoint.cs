using System.Globalization;
using System.Text.Json.Serialization;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>A normalized focal point using the same precision as Dynamic Image URLs.</summary>
public readonly record struct DynamicImageFocalPoint
{
	/// <summary>Gets the horizontal coordinate measured from the left.</summary>
	public double X { get; }
	/// <summary>Gets the vertical coordinate measured from the top.</summary>
	public double Y { get; }

	/// <summary>Creates a validated, canonical focal point.</summary>
	/// <param name="x">The horizontal coordinate.</param>
	/// <param name="y">The vertical coordinate.</param>
	[JsonConstructor]
	public DynamicImageFocalPoint(double x, double y)
	{
		X = Normalize(x);
		Y = Normalize(y);
	}

	/// <summary>Validates and rounds a coordinate to URL precision.</summary>
	/// <param name="value">The coordinate.</param>
	/// <returns>The canonical coordinate.</returns>
	public static double Normalize(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
			throw new ArgumentOutOfRangeException(nameof(value), "A focal coordinate must be finite and between zero and one.");

		return value is 0 ? 0 : double.Parse(value.ToString("G4", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
	}
}
