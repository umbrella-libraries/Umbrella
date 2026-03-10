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

	/// <inheritdoc />
	protected override bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out T result, [NotNullWhen(false)] out string? validationErrorMessage)
	{
		if (!base.TryParseValueFromString(value, out result, out validationErrorMessage))
			return false;

		result = NormalizeValue(result);

		return true;
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
		if (CurrentValue is null)
			return string.Empty;

		T normalizedValue = NormalizeValue(CurrentValue);

		// Use InvariantCulture for raw value to avoid currency symbols, etc.
		return Convert.ToString(normalizedValue, CultureInfo.InvariantCulture) ?? string.Empty;
	}

	private T NormalizeValue(T? value)
	{
		if (value is null || !TryGetFractionDigits(out int fractionDigits))
			return value!;

		return value switch
		{
			decimal decimalValue => (T)(object)RoundDecimal(decimalValue, fractionDigits),
			double doubleValue => (T)(object)RoundDouble(doubleValue, fractionDigits),
			float floatValue => (T)(object)RoundFloat(floatValue, fractionDigits),
			_ => value
		};
	}

	private bool TryGetFractionDigits(out int fractionDigits)
	{
		fractionDigits = 0;

		string? numericFormat = GetNumericFormat(Format);

		if (string.IsNullOrWhiteSpace(numericFormat))
			return false;

		ReadOnlySpan<char> span = numericFormat.AsSpan();

		if (!char.IsLetter(span[0]))
			return false;

		for (int index = 1; index < span.Length; index++)
		{
			if (!char.IsDigit(span[index]))
				return false;
		}

		int? precision = span.Length > 1 ? int.Parse(span[1..], CultureInfo.InvariantCulture) : null;

		switch (char.ToUpperInvariant(span[0]))
		{
			case 'C':
				fractionDigits = precision ?? CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalDigits;
				return true;
			case 'E':
				fractionDigits = precision ?? 6;
				return true;
			case 'F':
				fractionDigits = precision ?? 2;
				return true;
			case 'N':
				fractionDigits = precision ?? CultureInfo.CurrentCulture.NumberFormat.NumberDecimalDigits;
				return true;
			case 'P':
				fractionDigits = precision ?? CultureInfo.CurrentCulture.NumberFormat.PercentDecimalDigits;
				return true;
			default:
				return false;
		}
	}

	private static string? GetNumericFormat(string? format)
		=> string.IsNullOrWhiteSpace(format)
			? null
			: TrySplitStandardFormat(format, out _, out string standardFormat, out _)
				? standardFormat
				: format;

	private static decimal RoundDecimal(decimal value, int fractionDigits)
		=> Math.Round(value, fractionDigits, MidpointRounding.AwayFromZero);

	private static double RoundDouble(double value, int fractionDigits)
		=> Math.Round(value, fractionDigits, MidpointRounding.AwayFromZero);

	private static float RoundFloat(float value, int fractionDigits)
		=> MathF.Round(value, fractionDigits, MidpointRounding.AwayFromZero);
}