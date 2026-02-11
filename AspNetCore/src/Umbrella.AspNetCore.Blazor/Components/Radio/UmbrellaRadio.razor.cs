


using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Forms;
using System.Globalization;

namespace Umbrella.AspNetCore.Blazor.Components.Radio;

/// <summary>
/// Serves as the base class of the <see cref="UmbrellaRadio{TValue}"/> component.
/// </summary>
public abstract class UmbrellaRadioBase<TValue> : InputBase<TValue>
{
	/// <summary>
	/// Gets or sets the ID of the checkbox. This is used to associate the checkbox input with it's label.
	/// </summary>
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>
	/// Gets or sets the name used to group a collection of radio buttons.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public string GroupName { get; set; } = null!;

	/// <summary>
	/// Gets or sets the label text for the radio button.
	/// </summary>
	[Parameter]
	public string Text { get; set; } = null!;

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		Guard.IsNotNullOrWhiteSpace(GroupName, nameof(GroupName));
	}

	/// <summary>
	/// Gets or sets the selected value.
	/// </summary>
	[Parameter]
	public TValue SelectedValue { get; set; } = default!;

	/// <summary>
	/// Called by the Blazor infrastructure when the <see cref="SelectedValue"/> changes.
	/// </summary>
	/// <param name="args">The <see cref="ChangeEventArgs"/>.</param>
	protected void OnValueChange(ChangeEventArgs args) => CurrentValueAsString = args.Value?.ToString();

	/// <inheritdoc />
	protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage)
	{
		bool success = BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out var parsedValue);

		if (success && parsedValue is not null)
		{
			result = parsedValue;
			validationErrorMessage = null!;

			return true;
		}
		else
		{
			result = default!;
			validationErrorMessage = $"{FieldIdentifier.FieldName} field isn't valid.";

			return false;
		}
	}
}