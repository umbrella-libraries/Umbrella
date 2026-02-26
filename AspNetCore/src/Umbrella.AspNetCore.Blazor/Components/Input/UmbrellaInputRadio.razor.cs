using Microsoft.AspNetCore.Components.Forms;

namespace Umbrella.AspNetCore.Blazor.Components.Input;

/// <summary>
/// A component that represents a radio button input element. This component is designed to be used within a group of
/// radio buttons using the <see cref="InputRadioGroup{TValue}"/> component, allowing users to select one option from a
/// set of choices. It allows for customization of the label, CSS classes, and additional attributes for the input
/// element.
/// </summary>
/// <remarks>
/// If the type of <typeparamref name="TValue"/> is an enum, and values have not been provided for both the
/// <see cref="Label"/> and <see cref="ChildContent"/> properties, the
/// <see cref="EnumExtensions.ToDisplayString(Enum)" /> method will be used to generate a display string for the enum value.
/// </remarks>
public partial class UmbrellaInputRadio<TValue>
{
	/// <summary>
	/// Gets or sets the value of the radio button. This value is used to determine which radio button is selected within a
	/// group of radio buttons. When the user selects this radio button, the value will be assigned to the bound property
	/// in the parent <see cref="InputRadioGroup{TValue}"/> component.
	/// </summary>
	[Parameter]
	public TValue Value { get; set; } = default!;

	/// <summary>
	/// Gets or sets the label for the radio button. This label is displayed next to the radio button and provides a
	/// description of the option that the radio button represents. It can be used to enhance the user interface and
	/// improve accessibility by providing context for the radio button's purpose.
	/// </summary>
	[Parameter]
	public string? Label { get; set; }

	/// <summary>
	/// Gets or sets the content to be rendered inside the label. If a value for the <see cref="Label"/> parameter is
	/// provided, this property will be ignored.
	/// </summary>
	[Parameter]
	public RenderFragment? ChildContent { get; set; }

	/// <summary>
	/// The optional id applied to the radio button input and used as the <c> for</c> attribute of the label. If not
	/// specified, an arbitrary GUID will be used.
	/// </summary>
	[Parameter]
	public string? Id { get; set; }

	/// <summary>
	/// An optional css class applied to the component's container.
	/// </summary>
	[Parameter]
	public string? CssClass { get; set; }

	/// <summary>
	/// Additional attributes that will be applied to the radio button input element. This allows for customization
	/// and extension of the component by adding attributes such as <c>aria-label</c>, <c>data-* </c> attributes, etc.
	/// </summary>
	[Parameter(CaptureUnmatchedValues = true)]
	public Dictionary<string, object>? AdditionalAttributes { get; set; }
}