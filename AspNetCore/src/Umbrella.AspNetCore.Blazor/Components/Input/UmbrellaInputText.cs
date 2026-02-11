using Microsoft.AspNetCore.Components.Forms;

namespace Umbrella.AspNetCore.Blazor.Components.Input;

/// <summary>
/// An input component for editing <see cref="string"/> values.
/// </summary>
/// <remarks>
/// This extends the built-in <see cref="InputText"/> component
/// by setting some default values for some attributes as follows:
/// <list type="table">
/// <item>
/// <term>autocomplete</term>
/// <description>Defaults to <c>off</c></description>
/// </item>
/// <item>
/// <term>spellcheck</term>
/// <description>Defaults to <c>false</c></description>
/// </item>
/// <item>
/// <term>placeholder</term>
/// <description>
/// Defaults to the return value of a method call to <see cref="ExpressionExtensions.GetDisplayText(System.Linq.Expressions.LambdaExpression)"/> on the <see cref="InputBase{String}.ValueExpression"/>
/// Please see the documentation for the <c>GetDisplayText</c> method for more details.
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <seealso cref="InputText" />
public class UmbrellaInputText : InputText
{
	/// <inheritdoc />
	protected override void OnParametersSet() => AdditionalAttributes = UmbrellaInputHelper.ApplyAttributes(AdditionalAttributes, ValueExpression);
}