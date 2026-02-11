using System.Collections;
using System.Reflection;
using Humanizer;
using Umbrella.TypeScript.Annotations.Attributes;
using Umbrella.TypeScript.Annotations.Enumerations;
using Umbrella.Utilities.Extensions;

namespace Umbrella.TypeScript;

/// <summary>
/// A utility used to generated TypeScript types from .NET types.
/// </summary>
public static class TypeScriptUtility
{
	#region Public Static Methods		
	/// <summary>
	/// Generates the name of the type.
	/// </summary>
	/// <param name="memberName">Name of the member.</param>
	/// <param name="memberType">Type of the member.</param>
	/// <param name="outputModelType">Type of the output model.</param>
	/// <returns>The name of the type to be generated.</returns>
	public static string GenerateTypeName(string memberName, Type memberType, TypeScriptOutputModelType outputModelType)
	{
		if (memberType is null)
			throw new ArgumentNullException(nameof(memberType));

		string generatedName = memberName;

		if (!memberType.IsInterface && (outputModelType == TypeScriptOutputModelType.Interface
			|| outputModelType == TypeScriptOutputModelType.KnockoutInterface
			|| outputModelType == TypeScriptOutputModelType.AureliaInterface))
		{
			generatedName = "I" + generatedName;
		}

		if (outputModelType is TypeScriptOutputModelType.KnockoutClass or TypeScriptOutputModelType.KnockoutInterface)
		{
			int idxModelString = generatedName.LastIndexOf("Model", StringComparison.Ordinal);

			generatedName = idxModelString > -1
				? generatedName.Insert(idxModelString, "Knockout")
				: generatedName + "KnockoutModel";
		}

		return generatedName;
	}

	/// <summary>
	/// Gets the member information for a specific member on the .NET type for use with the TypeScript generator.
	/// </summary>
	/// <param name="modelType">Type of the model.</param>
	/// <param name="memberType">Type of the member.</param>
	/// <param name="propertyInfo">The property information.</param>
	/// <param name="outputType">Type of the output.</param>
	/// <param name="strictNullChecks">if set to <c>true</c> [strict null checks].</param>
	/// <param name="propertyMode">The property mode.</param>
	/// <returns>The <see cref="TypeScriptMemberInfo"/>.</returns>
	[Obsolete("This is obsolete for some reason.")]
	public static TypeScriptMemberInfo GetTypeScriptMemberInfo(Type modelType, Type memberType, PropertyInfo propertyInfo, TypeScriptOutputModelType outputType, bool strictNullChecks, TypeScriptPropertyMode propertyMode)
	{
		if (modelType is null)
			throw new ArgumentNullException(nameof(modelType));

		if (memberType is null)
			throw new ArgumentNullException(nameof(memberType));

		if (propertyInfo is null)
			throw new ArgumentNullException(nameof(propertyInfo));

		string memberName = propertyInfo.Name.Camelize();

		var info = new TypeScriptMemberInfo(memberName, memberType);

		// The primitive types are Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, IntPtr, UIntPtr, Char, Double, and Single.
		// Not checking for the use of IntPtr and UIntPtr. Assuming they just won't be used!
		if (memberType.IsPrimitive)
		{
			if (memberType == typeof(bool))
			{
				info.TypeName = "boolean";
			}
			else if (memberType == typeof(char))
			{
				info.TypeName = "string";
				info.IsNullable = true;
			}
			else
			{
				info.TypeName = "number";
			}
		}
		else
		{

			if (memberType == typeof(string))
			{
				info.TypeName = "string";
				info.IsNullable = true;
			}
			else if (memberType == typeof(DateTime))
			{
				// Always deal with DateTime instances as strings and deal with them on the client
				info.TypeName = "string";
				info.IsNullable = true;
			}
			else if (memberType == typeof(decimal))
			{
				info.TypeName = "number";
			}
			else if (memberType.IsNullableType())
			{
				// Get the underlying primitive type or struct
				Type underlyingType = Nullable.GetUnderlyingType(memberType);

				info = GetTypeScriptMemberInfo(modelType, underlyingType, propertyInfo, outputType, strictNullChecks, propertyMode);
				info.IsNullable = true;
			}
			else if (memberType == typeof(object))
			{
				info.TypeName = "any";
				info.IsNullable = true;
			}
			else if (memberType.IsArray)
			{
				// Strip the [] from the name and try and get the type
				string arrayTypeName = memberType.FullName.Replace("[]", "");

				var arrayType = Type.GetType(arrayTypeName);

				info = GetTypeScriptMemberInfo(modelType, arrayType, propertyInfo, outputType, strictNullChecks, propertyMode);

				// Set the type name correctly
				info.TypeName += "[]";
				info.IsNullable = true;
			}
			else if (memberType.IsAssignableToGenericType(typeof(IDictionary<,>)))
			{
				Type[] genericArgs = memberType.GetGenericArguments();

				Type keyType = genericArgs[0];
				Type valueType = genericArgs[1];

				var keyInfo = GetTypeScriptMemberInfo(modelType, keyType, propertyInfo, outputType, strictNullChecks, propertyMode);
				var valueInfo = GetTypeScriptMemberInfo(modelType, valueType, propertyInfo, outputType, strictNullChecks, propertyMode);

				info = keyInfo;

				// Set the type name correctly
				info.TypeName = $"Map<{keyInfo.TypeName}, {valueInfo.TypeName}>";
				info.IsNullable = true;
			}
			else if (typeof(IEnumerable).IsAssignableFrom(memberType))
			{
				if (memberType.IsGenericType)
				{
					// Determine the type of the collection
					Type genericEnumerableType = memberType.GetGenericArguments().First();

					info = GetTypeScriptMemberInfo(modelType, genericEnumerableType, propertyInfo, outputType, strictNullChecks, propertyMode);

					// Set the type name correctly
					info.TypeName += "[]";
				}
				else
				{
					// Not dealing with a generic collection - just output the type as "any[]";
					info.TypeName = "any[]";
				}

				info.IsNullable = true;
			}
			else if (memberType.IsEnum)
			{
				// Output the fully qualified name of the Enum in case it resides in another TypeScript namespace
				info.TypeName = memberType.FullName;
			}
			else
			{
				//We must be dealing with a class, e.g. a user defined child model

				// We need to generate the name for this member which will be output on the TypeScript model
				// Assume that the child class is being generated with the same output type as the parent, e.g. if the parent is a KnockoutClass, assume the child
				// has a Knockout representation.
				// TODO: Could provide an additional attribute to apply to a property to override this behaviour
				// TODO: Could also validate to see if the child model has the representation we need be getting hold of the attribute, or even checking it just exists.

				string generatedName = GenerateTypeName(memberType.Name, memberType, outputType);

				//Ensure the name that is output is fully qualified by namespace as it may not necessarily exist in the same one as the parent
				info.TypeName = memberType.Namespace + "." + generatedName;
				info.IsNullable = true;
				info.IsUserDefinedType = true;
			}
		}

		// If the type cannot be determined, default it to "any"
		if (string.IsNullOrEmpty(info.TypeName))
			info.TypeName = "any";

		// Set the initial output value - but only for non-interfaces
		if (!modelType.IsInterface)
		{
			if (propertyMode == TypeScriptPropertyMode.None)
			{
				// Don't need to do anything here as the InitialOutputValue will be null
			}
			else if (propertyMode == TypeScriptPropertyMode.Null)
			{
				info.InitialOutputValue = "null";
			}
			else if (propertyMode == TypeScriptPropertyMode.Model)
			{
				TypeScriptNullAttribute attrNull = propertyInfo.GetCustomAttribute<TypeScriptNullAttribute>();

				if (attrNull is not null)
				{
					info.InitialOutputValue = "null";
					info.IsNullable = true;

					return info;
				}

				object instance = Activator.CreateInstance(modelType);

				object propertyValue = propertyInfo.GetValue(instance);

				if (propertyValue is null)
				{
#pragma warning disable CA1508 // Avoid dead conditional code
					if (info.TypeName?.EndsWith("[]", StringComparison.Ordinal) is true && propertyInfo.GetCustomAttribute<TypeScriptEmptyAttribute>() is not null)
					{
						info.InitialOutputValue = "[]";
					}
					else
					{
						info.InitialOutputValue = "null";
						info.IsNullable = true;
					}
#pragma warning restore CA1508 // Avoid dead conditional code
				}
				else if (info.CLRType.IsEnum)
				{
					string name = Enum.GetName(info.CLRType, propertyValue);

					// An enum name will be null if it doesn't have a member with a value of 0
					// which propertyValue defaults to in the case of enums.
					// Where this is the case we need to manually get the first enum value and use that.
					if (string.IsNullOrEmpty(name))
						name = Enum.GetValues(info.CLRType).GetValue(0).ToString();

					info.InitialOutputValue = $"{info.CLRType.FullName}.{name}";
				}
				else if (info.CLRType == typeof(DateTime))
				{
					var dtPropertyValue = (DateTime)propertyValue;

					if (dtPropertyValue == default)
					{
						//Output unspecified dates as null rather than a string with DateTime.MinValue
						info.InitialOutputValue = "null";
						info.IsNullable = true;
					}
					else
					{
						// The only sensible way to output a DateTime value is in UTC format to ensure
						// that it is timezone and locale agnostic
						info.InitialOutputValue = $"\"{dtPropertyValue.ToUniversalTime():O}\"";
					}
				}
#pragma warning disable CA1508 // Avoid dead conditional code
				else if (info.CLRType == typeof(bool))
				{
					info.InitialOutputValue = propertyValue.ToString().ToLowerInvariant();
				}
				else if (info.TypeName?.EndsWith("[]", StringComparison.Ordinal) is true)
				{
					info.InitialOutputValue = "[]";
				}
				else if (info.TypeName?.StartsWith("Map<", StringComparison.Ordinal) is true)
				{
					// Maps are used to represent a Dictionary on the server. We can only support the default
					// empty Dictionary which can be done by instantiating a new Map.
					info.InitialOutputValue = $"new {info.TypeName}()";
				}
				else if (info.IsUserDefinedType)
				{
					info.InitialOutputValue = $"new {info.TypeName}()";
				}
				else
				{
					// For all other cases just output the property value
					info.InitialOutputValue = propertyValue.ToString();
				}
#pragma warning restore CA1508 // Avoid dead conditional code
			}
		}

		return info;
	}

	/// <summary>
	/// Gets the interface names for the specified model.
	/// </summary>
	/// <param name="modelType">Type of the model.</param>
	/// <param name="outputType">Type of the output.</param>
	/// <param name="includeSelf">if set to <see langword="true"/> the name of the <paramref name="modelType"/> is included in the generated names list.</param>
	/// <returns>The interface names.</returns>
	public static List<string> GetInterfaceNames(Type modelType, TypeScriptOutputModelType outputType, bool includeSelf)
	{
		if (modelType is null)
			throw new ArgumentNullException(nameof(modelType));

		var lstInterfaceName = new List<string>();

		// This interface is the primary interface which corresponds exactly to the modelType
		// Check the modelType isn't an interface first to avoid making the interface extend itself!
		if (includeSelf)
			lstInterfaceName.Add(GenerateTypeName(modelType.Name, modelType, outputType));

		// All other interfaces
		foreach (Type tInterface in modelType.GetInterfaces())
		{
			//Check if the interface has the same namespace as the model type to avoid outputting fully qualified names where possible
			string interfaceName = tInterface.Namespace == modelType.Namespace
				? tInterface.Name
				: tInterface.FullName;

			lstInterfaceName.Add(GenerateTypeName(interfaceName, tInterface, outputType));
		}

		// Ensure we have a distinct list
		return lstInterfaceName.Distinct().ToList();
	}
	#endregion
}