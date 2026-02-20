using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Umbrella.AspNetCore.Blazor.Components.Input;

/// <summary>
/// An input component for editing numerical values that have a formatted display.
/// </summary>
/// <typeparam name="T">The type of the numerical value.</typeparam>
public class UmbrellaFormattedInputNumber<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : InputNumber<T>
{
	/// <summary>
	/// The number format to use when displaying the value.
	/// </summary>
	/// <remarks>Defaults to <c>N2</c>.</remarks>
	[Parameter]
	public string Format { get; set; } = "N2";

	private bool _isFocused;

	/// <inheritdoc />
	protected override void BuildRenderTree(RenderTreeBuilder builder)
	{
		Guard.IsNotNull(builder);

		int sequence = 0;

		builder.OpenElement(sequence++, "input");
		builder.AddMultipleAttributes(sequence++, AdditionalAttributes);
		builder.AddAttribute(sequence++, "type", "text");

		if (AdditionalAttributes?.ContainsKey("id") is not true && ValueExpression is not null)
			builder.AddAttribute(sequence++, "id", ValueExpression.GetMemberPath());

		builder.AddAttribute(sequence++, "class", CssClass);
		string? displayValue = _isFocused ? GetRawValueAsString() : FormatValueAsString(CurrentValue); // Use nullable string
		builder.AddAttribute(sequence++, "value", displayValue);
		builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.CreateBinder<string>(
			this, value => CurrentValueAsString = value, displayValue ?? string.Empty));

		builder.AddAttribute(sequence++, "onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, OnFocus));
		builder.AddAttribute(sequence++, "onblur", EventCallback.Factory.Create<FocusEventArgs>(this, OnBlur));
		builder.CloseElement();
	}

	/// <inheritdoc />
	protected override string FormatValueAsString(T? value)
	{
		if (value is null)
			return string.Empty;

		if (TrySplitStandardFormat(Format, out string? prefix, out string? standardFormat, out string? suffix))
		{
			string formattedValue = string.Format(CultureInfo.CurrentCulture, $"{{0:{standardFormat}}}", value);
			return string.Concat(prefix, formattedValue, suffix);
		}

		// Format the value using the specified format
		return string.Format(CultureInfo.CurrentCulture, $"{{0:{Format}}}", value);
	}

	private void OnFocus(FocusEventArgs _)
	{
		_isFocused = true;
		StateHasChanged();
	}

	private void OnBlur(FocusEventArgs _)
	{
		_isFocused = false;
		StateHasChanged();
	}

	private static bool TrySplitStandardFormat(string? format, out string prefix, out string standardFormat, out string suffix)
	{
		prefix = string.Empty;
		standardFormat = string.Empty;
		suffix = string.Empty;

		if (string.IsNullOrWhiteSpace(format))
			return false;

		var span = format.AsSpan();
		int firstLetterIndex = -1;

		for (int index = 0; index < span.Length; index++)
		{
			if (!char.IsLetter(span[index]))
				continue;

			if (firstLetterIndex >= 0)
				return false;

			firstLetterIndex = index;
		}

		if (firstLetterIndex < 0)
			return false;

		int digitEnd = firstLetterIndex + 1;

		while (digitEnd < span.Length && char.IsDigit(span[digitEnd]))
			digitEnd++;

		standardFormat = format[firstLetterIndex..digitEnd];
		prefix = format[..firstLetterIndex];
		suffix = format[digitEnd..];

		return standardFormat.Length > 0 && (prefix.Length > 0 || suffix.Length > 0);
	}

	private string GetRawValueAsString()
	{
		// Show the raw value (unformatted) for editing
		if (Value is null)
			return string.Empty;

		// Use InvariantCulture for raw value to avoid currency symbols, etc.
		return Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
	}
}