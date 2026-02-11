


using System.Text.Json;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities;

/// <summary>
/// Static methods used by the Umbrella libraries which allow the default implementations to be changed.
/// </summary>
public static class UmbrellaStatics
{
	private static readonly JsonSerializerOptions _defaultOptions = new();

	private static readonly JsonSerializerOptions _camelCaseOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private static readonly JsonSerializerOptions _ignoreCaseOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	/// <summary>
	/// The delegate definition used to perform JSON serialization.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="useCamelCasingRules">if set to <see langword="true"/>, camel case will be used for property names.</param>
	/// <returns>The JSON string.</returns>
	public delegate string SerializeJsonDelegate(object value, bool useCamelCasingRules);

	/// <summary>
	/// The delegate definition used to perform JSON deserialization.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="type">The type.</param>
	/// <returns>The deserialized object.</returns>
	public delegate object? DeserializeJsonDelegate(string value, Type type);

	/// <summary>
	/// Gets or sets the JSON serializer implementation. Defaults to <see cref="DefaultSerialize(object, bool)"/>.
	/// </summary>
	public static SerializeJsonDelegate JsonSerializerImplementation { private get; set; } = DefaultSerialize;

	/// <summary>
	/// Gets or sets the JSON deserializer implementation. Defaults to <see cref="DefaultDeserialize(string, Type)"/>.
	/// </summary>
	public static DeserializeJsonDelegate JsonDeserializerImplementation { private get; set; } = DefaultDeserialize;

	/// <summary>
	/// Serializes the specified <paramref name="value"/> to JSON.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="useCamelCasingRules">if set to <see langword="true"/>, camel case will be used for property names.</param>
	/// <returns>The JSON string.</returns>
	/// <exception cref="UmbrellaException">The JsonSerializer has not been assigned. This should be done on application startup.</exception>
	public static string SerializeJson(object value, bool useCamelCasingRules = false)
	{
		Guard.IsNotNull(value);

		return JsonSerializerImplementation is null
			? throw new UmbrellaException("The JsonSerializer has not been assigned. This should be done on application startup.")
			: JsonSerializerImplementation(value, useCamelCasingRules);
	}

	/// <summary>
	/// Deserializes the JSON to the specified type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <param name="value">The value.</param>
	/// <returns>The deserialized object.</returns>
	/// <exception cref="UmbrellaException">The JsonDeserializer has not been assigned. This should be done on application startup.</exception>
	public static T? DeserializeJson<T>(string value)
	{
		Guard.IsNotNullOrWhiteSpace(value);

		return JsonDeserializerImplementation is null
			? throw new UmbrellaException("The JsonDeserializer has not been assigned. This should be done on application startup.")
			: (T?)JsonDeserializerImplementation(value, typeof(T));
	}

	/// <summary>
	/// Serializes the specified <paramref name="value"/> to JSON.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="useCamelCasingRules">if set to <see langword="true"/>, camel case will be used for property names.</param>
	/// <returns>The JSON string.</returns>
	public static string DefaultSerialize(object value, bool useCamelCasingRules) => JsonSerializer.Serialize(value, value.GetType(), useCamelCasingRules ? _camelCaseOptions : _defaultOptions);

	/// <summary>
	/// Deserializes the JSON to the specified type <paramref name="type"/>.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="type">The type.</param>
	/// <returns>The deserialized object.</returns>
	public static object? DefaultDeserialize(string value, Type type) => JsonSerializer.Deserialize(value, type, _ignoreCaseOptions);
}