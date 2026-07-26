using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.Localization;
using Umbrella.DataAnnotations;

namespace Umbrella.AspNetCore.DataAnnotations;

/// <summary>
/// A custom validation adapter provider to allow custom <see cref="ValidationAttribute"/>s to be registered with the ASP.NET Core infrastructure. The primary purpose of this
/// is to enable client-side validation using the default validation mechanism built into ASP.NET Core.
/// </summary>
/// <seealso cref="IValidationAttributeAdapterProvider" />
internal sealed class UmbrellaValidationAttributeAdapterProvider : IValidationAttributeAdapterProvider
{
	#region Private Members
	private readonly ValidationAttributeAdapterProvider _mvcFrameworkImplementation = new();
	#endregion

	#region IValidationAttributeAdapterProvider Members
	/// <inheritdoc />
	public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
	{
		IAttributeAdapter? adapter = _mvcFrameworkImplementation.GetAttributeAdapter(attribute, stringLocalizer);

		if (adapter is null)
		{
			Type type = attribute.GetType();

			if (type == typeof(UmbrellaPostcodeAttribute) || type == typeof(UmbrellaPhoneAttribute))
			{
				// TODO: Is this still needed? Or will it just work?
				// adapter = new RegularExpressionAttributeAdapter((RegularExpressionAttribute)attribute, stringLocalizer);
			}
			else
			{
				// Need to create adapter implementations of all the other attributes in the Umbrella.DataAnnotations library in order
				// for them to work properly with jQuery unobtrusive validation as they do currently in MVC 5 using the Umbrella.Legacy.WebUtilities.DataAnnotations library.
			}
		}

#pragma warning disable CS8603 // Possible null reference return.
		return adapter;
#pragma warning restore CS8603 // Possible null reference return.
	}
	#endregion
}
