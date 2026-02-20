using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;

namespace Umbrella.AspNetCore.Blazor.Components.Input;

/// <summary>
/// An input component for editing date values. Supported types are <see cref="DateTime"/> and
/// <see cref="DateTimeOffset"/> .
/// </summary>
/// <remarks>
/// This extends the built-in <see cref="InputDate{TValue}"/> component by setting some default values for some
/// attributes as follows: <list type="table"> <item> <term> autocomplete</term> <description> Defaults to <c>
/// off</c></description> </item> <item> <term> spellcheck</term> <description> Defaults to <c> false</c></description>
/// </item> <item> <term> placeholder</term> <description> Defaults to the return value of a method call to
/// <see cref="ExpressionExtensions.GetDisplayText(System.Linq.Expressions.LambdaExpression)"/> on the
/// <see cref="InputBase{TValue}.ValueExpression"/> Please see the documentation for the <c> GetDisplayText</c> method
/// for more details. </description> . </item> </list>
/// </remarks>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <seealso cref="InputDate{TValue}" />
public class UmbrellaInputDate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : InputDate<TValue>
{
	/// <inheritdoc />
	protected override void OnParametersSet() => AdditionalAttributes = UmbrellaInputHelper.ApplyAttributes(AdditionalAttributes, ValueExpression);
}