using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers.Knockout;

/// <summary>
/// A tag helper that output Knockout bindings inside the 'data-bind' attribute.
/// </summary>
/// <seealso cref="TagHelper" />
[HtmlTargetElement("input", Attributes = PropertyNameAttributeName)]
[HtmlTargetElement("textarea", Attributes = PropertyNameAttributeName)]
public class KnockoutPropertyTagHelper : TagHelper
{
	/// <summary>
	/// The property name attribute name.
	/// </summary>
	protected const string PropertyNameAttributeName = "ko-property";

	/// <summary>
	/// The type of the Knockout property.
	/// </summary>
	public enum KnockoutPropertyType
	{
		/// <summary>
		/// Uses the 'value' binding.
		/// </summary>
		Default,

		/// <summary>
		/// Uses the 'numericValue' binding.
		/// </summary>
		Numeric,

		/// <summary>
		/// Uses the 'floatingValue' binding.
		/// </summary>
		Floating,

		/// <summary>
		/// Uses the 'currencyValue' binding.
		/// </summary>
		Currency,

		/// <summary>
		/// Uses the 'dateTimePicker' binding.
		/// </summary>
		DateTimePicker,

		/// <summary>
		/// Uses the 'checked' binding.
		/// </summary>
		Checked,

		/// <summary>
		/// Uses the 'textInput' binding.
		/// </summary>
		TextInput
	}

	private readonly ILogger _logger;
	private readonly Lazy<string> _exposedPropertyName;

	/// <summary>
	/// Gets or sets the name of the property on the KO model.
	/// </summary>
	[HtmlAttributeName(PropertyNameAttributeName)]
	public string? PropertyName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this KO property should be validated. Defaults to <see langword="true" />.
	/// </summary>
	[HtmlAttributeName("ko-validate")]
	public bool Validate { get; set; } = true;

	/// <summary>
	/// Gets or sets the name of the property that controls whether this control is disabled or not.
	/// </summary>
	[HtmlAttributeName("ko-disabled")]
	public string? DisabledPropertyName { get; set; }

	/// <summary>
	/// Gets or sets the type of the property. Defaults to <see cref="KnockoutPropertyType.Default"/>.
	/// </summary>
	[HtmlAttributeName("ko-property-type")]
	public KnockoutPropertyType PropertyType { get; set; } = KnockoutPropertyType.Default;

	/// <summary>
	/// Gets or sets the date picker options when the <see cref="PropertyType" /> is set to <see cref="KnockoutPropertyType.DateTimePicker" />.
	/// </summary>
	[HtmlAttributeName("ko-datetime-picker-options")]
	public string? DateTimePickerOptions { get; set; }

	/// <summary>
	/// Gets or sets the id of the HTML input element to link this element to.
	/// </summary>
	[HtmlAttributeName("ko-datetime-picker-linked")]
	public string? DateTimePickerLinked { get; set; }

	/// <summary>
	/// Gets or sets the value of the 'checkedValue' binding used in conjunction with the 'checked' binding.
	/// Used when the <see cref="PropertyType" /> is set to <see cref="KnockoutPropertyType.Checked" />.
	/// </summary>
	[HtmlAttributeName("ko-checked-value")]
	public string? CheckedValue { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="KnockoutPropertyTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public KnockoutPropertyTagHelper(
		ILogger<KnockoutPropertyTagHelper> logger)
	{
		_logger = logger;

		_exposedPropertyName = new Lazy<string>(() =>
		{
			if (string.IsNullOrWhiteSpace(PropertyName))
				return string.Empty;

			string? exposedPropertyName = null;
			int periodIdx = PropertyName.LastIndexOf('.');

			if (periodIdx > -1)
			{
				int sliceLength = periodIdx + 1;

				ReadOnlySpan<char> propertyNameSpan = PropertyName;
				Span<char> span = stackalloc char[PropertyName.Length + 1];
				int idx = span.Write(0, propertyNameSpan.Slice(0, sliceLength));
				idx = span.Write(idx, "_");
				_ = span.Write(idx, propertyNameSpan.Slice(sliceLength));

				exposedPropertyName = span.ToString();
			}
			else
			{
				exposedPropertyName = "_" + PropertyName;
			}

			return exposedPropertyName;
		});
	}

	/// <inheritdoc />
	public override void Process(TagHelperContext context, TagHelperOutput output)
	{
		ArgumentNullException.ThrowIfNull(output);

		try
		{
			if (!string.IsNullOrWhiteSpace(PropertyName))
			{
				string dataBindValue = PropertyType switch
				{
					KnockoutPropertyType.Numeric => $"numericValue: {_exposedPropertyName.Value}",
					KnockoutPropertyType.Floating => $"floatingValue: {_exposedPropertyName.Value}",
					KnockoutPropertyType.Currency => $"currencyValue: {_exposedPropertyName.Value}",
					KnockoutPropertyType.DateTimePicker => $"dateTimePicker: {PropertyName}",
					KnockoutPropertyType.Checked => $"checked: {PropertyName}",
					KnockoutPropertyType.TextInput => $"textInput: {PropertyName}",
					_ => $"value: {PropertyName}"
				};

				if (Validate)
				{
					dataBindValue += $", cssValid: {_exposedPropertyName.Value}";

					_ = output.PostElement.SetHtmlContent($"<div class=\"invalid-feedback\" data-bind=\"validationMessage: {_exposedPropertyName.Value}\"></div>");
				}

				if (!string.IsNullOrWhiteSpace(DisabledPropertyName))
					dataBindValue += $", disable: {DisabledPropertyName}";

				if (PropertyType == KnockoutPropertyType.DateTimePicker && !string.IsNullOrWhiteSpace(DateTimePickerOptions))
				{
					dataBindValue += $", dateTimePickerOptions: {{{DateTimePickerOptions}}}";

					if (!string.IsNullOrWhiteSpace(DateTimePickerLinked))
						dataBindValue += $", dateTimePickerLinked: '{DateTimePickerLinked}'";
				}
				else if (PropertyType == KnockoutPropertyType.Checked && !string.IsNullOrWhiteSpace(CheckedValue))
				{
					dataBindValue += $", checkedValue: {CheckedValue}";
				}

				output.Attributes.Add("data-bind", dataBindValue);
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { PropertyName, DisabledPropertyName, Validate }))
		{
			throw new UmbrellaWebException("An error occurred whilst processing the attribute.", exc);
		}
	}
}
