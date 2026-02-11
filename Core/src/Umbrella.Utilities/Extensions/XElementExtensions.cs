


using System.Globalization;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for the <see cref="XElement"/> class.
/// </summary>
public static class XElementExtensions
{
	/// <summary>
	/// Gets the attribute value with the specified <paramref name="name"/> from the <paramref name="element"/>.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="name">The name.</param>
	/// <param name="required">if set to <see langword="true"/>, an exception will be thrown if the attribute cannot be found.</param>
	/// <param name="fallback">The fallback value returned when <paramref name="required"/> is <see langword="false"/>.</param>
	/// <returns>The attribute value.</returns>
	public static string? GetAttributeValue(this XElement element, string name, bool required = true, string fallback = "")
		=> GetAttributeValue<string>(element, name, required, fallback);

	/// <summary>
	/// Gets the attribute value with the specified <paramref name="name"/> from the <paramref name="element"/>.
	/// </summary>
	/// <typeparam name="T">The type of the attribute value.</typeparam>
	/// <param name="element">The element.</param>
	/// <param name="name">The name.</param>
	/// <param name="required">if set to <see langword="true"/>, an exception will be thrown if the attribute cannot be found.</param>
	/// <param name="fallback">The fallback value returned when <paramref name="required"/> is <see langword="false"/>.</param>
	/// <param name="cultureInfo">The culture info.</param>
	/// <returns>The attribute value.</returns>
	/// <remarks>If <paramref name="cultureInfo"/> is not specified, <see cref="CultureInfo.CurrentCulture"/> will be used.</remarks>
	public static T GetAttributeValue<T>(this XElement element, string name, bool required = true, T fallback = default!, CultureInfo? cultureInfo = null)
	{
		Guard.IsNotNull(element, nameof(element));
		Guard.IsNotNullOrWhiteSpace(name, nameof(name));

		XAttribute? attribute = element.Attribute(name);

		if (attribute is null && required)
			throw new UmbrellaException($"The {name} attribute of a {element.Name} element could not be found.");

		string? attributeValue = attribute?.Value;

		var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

		if (!string.IsNullOrWhiteSpace(attributeValue))
		{
			if (type.IsPrimitive || type == typeof(decimal) || type == typeof(string))
			{
				string cleanedAttributeValue = type.IsPrimitive || type == typeof(decimal)
#if NET6_0_OR_GREATER
					? attributeValue!.Replace(" ", "", StringComparison.Ordinal)
#else
					? attributeValue!.Replace(" ", "")
#endif
					: attributeValue!;

				try
				{
					return (T)Convert.ChangeType(cleanedAttributeValue, type, cultureInfo ?? CultureInfo.CurrentCulture);
				}
				catch (Exception exc)
				{
					throw new UmbrellaException($"The {name} attribute of a {element.Name} element with value {attributeValue} could not be converted to type {type.FullName}.", exc);
				}
			}
		}

		return type == typeof(string) && fallback is null
			? (T)Convert.ChangeType(string.Empty, type, cultureInfo ?? CultureInfo.CurrentCulture)
			: fallback!;
	}

	/// <summary>
	/// Gets the value with the specified <paramref name="name"/> from the <paramref name="element"/> and converts to an enum
	/// of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type of the enum.</typeparam>
	/// <param name="element">The element.</param>
	/// <param name="name">The name.</param>
	/// <param name="required">if set to <see langword="true"/>, causes an exception to be thrown if the attribute cannot be found.
	/// If set to <see langword="false"/>, the <paramref name="fallback"/> is returned.
	/// </param>
	/// <param name="fallback">The fallback.</param>
	/// <returns>The enum value, or the fallback if it cannot be found.</returns>
	/// <exception cref="UmbrellaException">The {name} attribute of a {element.Name} element could not be found.</exception>
	public static T GetAttributeEnumValue<T>(this XElement element, string name, bool required = true, T fallback = default)
		where T : struct, Enum
	{
		Guard.IsNotNull(element, nameof(element));
		Guard.IsNotNullOrWhiteSpace(name, nameof(name));

		XAttribute? attribute = element.Attribute(name);

		if (attribute is null && required)
			throw new UmbrellaException($"The {name} attribute of a {element.Name} element could not be found.");

#if NET6_0_OR_GREATER
		string? attributeValue = attribute?.Value?.Replace(" ", "", StringComparison.Ordinal);
#else
		string? attributeValue = attribute?.Value?.Replace(" ", "");
#endif

		return !string.IsNullOrEmpty(attributeValue) && Enum.TryParse(attributeValue, true, out T output)
			? output
			: fallback;
	}

	/// <summary>
	/// Tries the get the attribute value with the specified <paramref name="name"/>.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="name">The name.</param>
	/// <param name="fallback">The fallback if the attribute cannot be found.</param>
	/// <returns>A tuple indicating success or failure together with the value or fallback.</returns>
	public static (bool success, string? value) TryGetAttributeValue(this XElement element, string name, string fallback = "")
	{
		try
		{
			return (true, GetAttributeValue(element, name, true, fallback));
		}
		catch (Exception)
		{
			return (false, fallback);
		}
	}

	/// <summary>
	/// Tries the get the attribute value with the specified <paramref name="name"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="element">The element.</param>
	/// <param name="name">The name.</param>
	/// <param name="fallback">The fallback if the attribute cannot be found.</param>
	/// <returns>A tuple indicating success or failure together with the value or fallback.</returns>
	public static (bool success, T value) TryGetAttributeValue<T>(this XElement element, string name, T fallback = default!)
	{
		try
		{
			return (true, GetAttributeValue(element, name, true, fallback)!);
		}
		catch (Exception)
		{
			return (false, fallback!);
		}
	}

	/// <summary>
	/// Tries the get the attribute value as an enum of type <typeparamref name="T"/> with the specified <paramref name="name"/>.
	/// </summary>
	/// <typeparam name="T">The type of the enum.</typeparam>
	/// <param name="element">The element.</param>
	/// <param name="name">The name.</param>
	/// <param name="fallback">The fallback if the attribute cannot be found.</param>
	/// <returns>A tuple indicating success or failure together with the value or fallback.</returns>
	public static (bool success, T value) TryGetAttributeEnumValue<T>(this XElement element, string name, T fallback = default)
		where T : struct, Enum
	{
		try
		{
			return (true, GetAttributeEnumValue(element, name, true, fallback));
		}
		catch (Exception)
		{
			return (false, fallback);
		}
	}
}