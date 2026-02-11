using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;

namespace Umbrella.Utilities.Data.Attributes;

/// <summary>
/// Used to specify a full property path to be sent to the server to override the property name
/// when a property is being used in conjunction with <see cref="SortExpression{T}"/> and <see cref="FilterExpression{T}"/> instances.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UmbrellaMemberPathAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaMemberPathAttribute"/> class.
	/// </summary>
	/// <param name="value">The value.</param>
	[SetsRequiredMembers]
	public UmbrellaMemberPathAttribute(string value)
	{
		Guard.IsNotNullOrWhiteSpace(value);

		Value = value;
	}

	/// <summary>
	/// Gets the value.
	/// </summary>
	public string Value { get; }
}