using System.Linq.Expressions;

namespace Umbrella.AspNetCore.Blazor.Components.Input;

internal static class UmbrellaInputHelper
{
	public static Dictionary<string, object> ApplyAttributes<TValue>(IReadOnlyDictionary<string, object>? additionalAttributes, Expression<Func<TValue>>? valueExpression)
	{
		Dictionary<string, object> dicAdditionalAttributes = additionalAttributes is not null ? new(additionalAttributes) : new();

		SetLazyAttribute("id", () => valueExpression?.GetMemberPath());
		SetLazyAttribute("placeholder", () => valueExpression?.GetDisplayText());
		SetAttribute("autocomplete", "off");
		SetAttribute("spellcheck", "false");
		SetAttribute("data-lpignore", "true");

		void SetLazyAttribute(string name, Func<string?> overrideDelegate)
		{
			if (!dicAdditionalAttributes.TryGetValue(name, out object? currentValue) || string.IsNullOrEmpty(currentValue?.ToString()))
			{
				string? value = overrideDelegate();

				if (!string.IsNullOrEmpty(value))
					dicAdditionalAttributes[name] = value;
			}
		}

		void SetAttribute(string name, string? overrideValue)
		{
			SetLazyAttribute(name, () => overrideValue);
		}

		return dicAdditionalAttributes;
	}
}