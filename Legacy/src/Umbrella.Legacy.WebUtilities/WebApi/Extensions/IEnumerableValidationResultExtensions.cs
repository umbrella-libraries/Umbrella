
using System.ComponentModel.DataAnnotations;
using System.Web.Http.ModelBinding;

namespace Umbrella.Legacy.WebUtilities.WebApi.Extensions;

/// <summary>
/// Extension methods that operation on enumerable collections of <see cref="ValidationResult"/>.
/// </summary>
public static class IEnumerableValidationResultExtensions
{
	/// <summary>
	/// Converts an enumerable collection of <see cref="ValidationResult"/> to a <see cref="ModelStateDictionary"/>.
	/// </summary>
	/// <param name="validationResults">The validation results.</param>
	/// <returns></returns>
	public static ModelStateDictionary ToModelStateDictionary(this IEnumerable<ValidationResult> validationResults)
	{
		if (validationResults is null)
			throw new ArgumentNullException(nameof(validationResults));

		var dictionary = new ModelStateDictionary();

		foreach (var item in validationResults)
		{
			var memberNames = item.MemberNames?.ToList();

			if (memberNames?.Count > 0)
			{
				foreach (string key in memberNames)
				{
					dictionary.AddModelError(key, item.ErrorMessage);
				}
			}
			else
			{
				dictionary.AddModelError(string.Empty, item.ErrorMessage);
			}
		}

		return dictionary;
	}
}
