using System.Globalization;
using System.Reflection;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for the <see cref="Type"/> class.
/// </summary>
public static class TypeExtensions
{
	/// <summary>
	/// Gets the public or private instance properties defined on the specified <paramref name="type"/> .
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The properties.</returns>
	public static PropertyInfo[] GetPublicOrPrivateProperties(this Type type)
	{
		Guard.IsNotNull(type);

		return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	/// <summary>
	/// Determines which of the specified <paramref name="types"/> are assignable to the specified
	/// <paramref name="superType"/> and returns a filtered collection containing the matches.
	/// </summary>
	/// <param name="types">The types.</param>
	/// <param name="superType">The super type.</param>
	/// <returns>A filtered collection containing the matches.</returns>
	public static IEnumerable<Type> AssignableTo(this IEnumerable<Type> types, Type superType)
	{
		Guard.IsNotNull(superType);

		return Enumerable.Where(types, new Func<Type, bool>(superType.IsAssignableFrom));
	}

	/// <summary>
	/// Filters the specified <paramref name="types"/> to remove any types marked as <see langword="abstract"/> .
	/// </summary>
	/// <param name="types">The types.</param>
	/// <returns>The filtered collection of concrete types.</returns>
	public static IEnumerable<Type> Concrete(this IEnumerable<Type> types) => Enumerable.Where(types, type => !type.IsAbstract);

	/// <summary>
	/// Determines whether the specified <paramref name="type"/> is <see cref="Nullable{T}"/> .
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>
	/// <see langword="true"/> if the specified <paramref name="type"/> is <see cref="Nullable{T}"/> ; otherwise,
	/// <see langword="false"/> .
	/// </returns>
	public static bool IsNullableType(this Type type)
	{
		Guard.IsNotNull(type);

		return type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>));
	}

	/// <summary>
	/// Determines whether the type is permitted to have a null value.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns><see langword="true"/> if it is; otherwise <see langword="false"/>.</returns>
	public static bool CanBeNull(this Type type)
	{
		Guard.IsNotNull(type);

		return !type.IsValueType || IsNullableType(type);
	}

	/// <summary>
	/// Gets a dictionary of value / name pairs for the specified enum type.
	/// </summary>
	/// <param name="enumType">Type of the enum.</param>
	/// <returns>A dictionary of value / name pairs.</returns>
	/// <exception cref="ArgumentException"></exception>
	public static Dictionary<int, string> GetEnumDictionary(this Type enumType)
	{
		Guard.IsNotNull(enumType);

		return !enumType.IsEnum
			? throw new ArgumentException($"{nameof(enumType)} is not an Enum.")
			: Enum.GetValues(enumType)
			.OfType<object>()
			.ToDictionary(x => Convert.ToInt32(x, CultureInfo.InvariantCulture), x => Enum.GetName(enumType, x) ?? throw new InvalidOperationException("There has been a problem getting the enum name."));
	}

	/// <summary>
	/// Determines whether the specified <paramref name="givenType"/> is assignable to the specified
	/// <paramref name="genericType"/> .
	/// </summary>
	/// <param name="givenType">The target type.</param>
	/// <param name="genericType">The generic type we want to assign to.</param>
	/// <returns>
	/// <see langword="true" /> if the <paramref name="givenType"/> can be assigned to the specified
	/// <paramref name="genericType"/> ; otherwise, <see langword="false" /> .
	/// </returns>
	public static bool IsAssignableToGenericType(this Type givenType, Type genericType)
	{
		Guard.IsNotNull(givenType);
		Guard.IsNotNull(genericType);

		var interfaceTypes = givenType.GetInterfaces();

		foreach (var it in interfaceTypes)
		{
			if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
				return true;
		}

		if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
			return true;

		Type? baseType = givenType.BaseType;

		return baseType is not null && IsAssignableToGenericType(baseType, genericType);
	}

	/// <summary>
	/// Gets the element type for a type which implements <see cref="IEnumerable{T}"/> .
	/// </summary>
	/// <param name="givenType">The type to inspect.</param>
	/// <returns>
	/// A tuple specifying if the <paramref name="givenType"/> implements <see cref="IEnumerable{T}"/> and if so, the type
	/// of its elements.
	/// </returns>
	public static (bool isEnumerable, Type? elementType) GetIEnumerableTypeData(this Type givenType)
	{
		Guard.IsNotNull(givenType);

		if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			return (true, givenType.GetGenericArguments()[0]);

		var interfaceTypes = givenType.GetInterfaces();

		foreach (var it in interfaceTypes)
		{
			if (it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				return (true, it.GetGenericArguments()[0]);
		}

		Type? baseType = givenType.BaseType;

		return baseType is null ? ((bool isEnumerable, Type? elementType))(false, null) : baseType.GetIEnumerableTypeData();
	}

	/// <summary>
	/// Gets the original type, if one exists, for the specified type in cases where the specified type has been
	/// dynamically proxied at runtime.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>
	/// The original type, if it has been dynamically proxied, otherwise the same type instance passed into the method.
	/// </returns>
	public static Type? GetOriginalType(this Type type) => type switch
	{
		var _ when type.Name?.EndsWith("_DynamicProxy", StringComparison.Ordinal) is true => type.BaseType,
		var _ when type.FullName?.StartsWith("Castle.Proxies", StringComparison.Ordinal) is true && type.Name?.EndsWith("Proxy", StringComparison.Ordinal) is true => type.BaseType,
		_ => type
	};
}