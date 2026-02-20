using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;

namespace Umbrella.AspNetCore.Blazor.Components.Input;

/// <summary>
/// A dropdown selection component.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public class UmbrellaInputSelect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : InputSelect<TValue>
{
	/// <inheritdoc />
	protected override void OnParametersSet() => AdditionalAttributes = UmbrellaInputHelper.ApplyAttributes(AdditionalAttributes, ValueExpression);
}