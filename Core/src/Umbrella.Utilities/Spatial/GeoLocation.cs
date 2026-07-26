namespace Umbrella.Utilities.Spatial;

// TODO PR: Make readonly when JsonConstructor becomes a thing in .NET 5.	
/// <summary>
/// Represents a location using latitude and longitude and allows for calculating distances to other <see cref="GeoLocation"/> instances.
/// </summary>
/// <seealso cref="IEquatable{GeoLocation}" />
public struct GeoLocation : IEquatable<GeoLocation>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GeoLocation"/> struct.
	/// </summary>
	/// <param name="latitude">The latitude.</param>
	/// <param name="longitude">The longitude.</param>
	public GeoLocation(double latitude, double longitude)
	{
		Latitude = latitude;
		Longitude = longitude;
	}

	/// <summary>
	/// Gets or sets the latitude.
	/// </summary>
	public double Latitude { readonly get; set; }

	/// <summary>
	/// Gets or sets the longitude.
	/// </summary>
	public double Longitude { readonly get; set; }

	/// <inheritdoc />
	public override readonly bool Equals(object? obj) => obj is GeoLocation location && Equals(location);

	/// <inheritdoc />
	public readonly bool Equals(GeoLocation other) => Latitude == other.Latitude &&
			   Longitude == other.Longitude;

	/// <inheritdoc />
#if NET8_0_OR_GREATER
	public override readonly int GetHashCode() => HashCode.Combine(Latitude, Longitude);
#else
#pragma warning disable IDE0070 // HashCode.Combine is unavailable on the legacy target frameworks.
	public override readonly int GetHashCode()
	{
		int hashCode = -1416534245;
		hashCode = (hashCode * -1521134295) + Latitude.GetHashCode();
		hashCode = (hashCode * -1521134295) + Longitude.GetHashCode();
		return hashCode;
	}
#pragma warning restore IDE0070
#endif

	/// <summary>
	/// Calculates the distance between the current instance and the specified <paramref name="other"/> <see cref="GeoLocation"/>.
	/// </summary>
	/// <param name="other">The other.</param>
	/// <param name="distanceType">Type of the distance.</param>
	/// <returns>The distance using the units specified by the <paramref name="distanceType"/> parameter.</returns>
	public readonly double DistanceTo(in GeoLocation other, in GeoLocationDistanceType distanceType = GeoLocationDistanceType.Meters)
	{
		const double piDeg180 = Math.PI / 180;

		double d1 = Latitude * piDeg180;
		double num1 = Longitude * piDeg180;
		double d2 = other.Latitude * piDeg180;
		double num2 = (other.Longitude * piDeg180) - num1;
		double d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + (Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0));

		double distanceMeters = 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));

		return distanceType switch
		{
			GeoLocationDistanceType.Meters => distanceMeters,
			GeoLocationDistanceType.Kilometers => distanceMeters / 1000,
			GeoLocationDistanceType.Miles => distanceMeters * 0.000621371,
			GeoLocationDistanceType.NauticalMiles => distanceMeters * 0.000539957,
			_ => distanceMeters
		};
	}

	/// <summary>
	/// Implements the operator ==.
	/// </summary>
	/// <param name="left">The left.</param>
	/// <param name="right">The right.</param>
	/// <returns>
	/// The result of the operator.
	/// </returns>
	public static bool operator ==(in GeoLocation left, in GeoLocation right) => left.Equals(right);

	/// <summary>
	/// Implements the operator !=.
	/// </summary>
	/// <param name="left">The left.</param>
	/// <param name="right">The right.</param>
	/// <returns>
	/// The result of the operator.
	/// </returns>
	public static bool operator !=(in GeoLocation left, in GeoLocation right) => !(left == right);
}
