
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Umbrella.AspNetCore.WebUtilities.Extensions;

/// <summary>
/// Extension methods that operate on <see cref="ModelStateDictionary" /> instances.
/// </summary>
public static class ModelStateDictionaryExtensions
{
	/// <summary>
	/// Adds the validation results to the model state dictionary.
	/// </summary>
	/// <param name="modelState">The model state dictionary.</param>
	/// <param name="validationResults">The validation results.</param>
	public static void AddValidationResults(this ModelStateDictionary modelState, IEnumerable<ValidationResult> validationResults)
	{
		foreach (var item in validationResults)
		{
			var memberNames = item.MemberNames?.ToList();

			if (memberNames?.Count > 0)
			{
				foreach (string key in memberNames)
				{
					modelState.AddModelError(key, item.ErrorMessage ?? string.Empty);
				}
			}
			else
			{
				if (!string.IsNullOrEmpty(item.ErrorMessage))
					modelState.AddModelError(string.Empty, item.ErrorMessage);
			}
		}
	}
}