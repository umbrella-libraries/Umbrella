
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Http;
using System.Web.Http.WebHost;

namespace Umbrella.Legacy.WebUtilities.WebApi.Validators;

/// <summary>
/// Used to define a custom status code to associate with customly defined validation attributes which will be thrown as a HttpResponseException
/// when processed through the Web API pipeline. The MVC pipeline will ignore and treat as a standard validation error.
/// </summary>
public abstract class StatusCodeValidationAttribute : ValidationAttribute
{
	private readonly HttpContext? _httpContext;

	/// <summary>
	/// Gets or sets the status code.
	/// </summary>
	public HttpStatusCode StatusCode { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="StatusCodeValidationAttribute"/> class.
	/// </summary>
	protected StatusCodeValidationAttribute()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StatusCodeValidationAttribute"/> class.
	/// </summary>
	/// <param name="context">The context.</param>
	protected StatusCodeValidationAttribute(HttpContext context)
	{
		_httpContext = context;
	}

	/// <summary>
	/// Handles the invalid result.
	/// </summary>
	/// <param name="validationContext">The validation context.</param>
	/// <returns>The validation result.</returns>
	/// <exception cref="HttpResponseException"></exception>
	protected ValidationResult HandleInvalidResult(ValidationContext validationContext)
	{
		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		// Ensure we have the HttpContext - always re-assign the instance variable if not testing
		// because these validation attributes get cached by the framework
		HttpContext context = _httpContext ?? HttpContext.Current;

		// Processing via Web API
		if (context.CurrentHandler is HttpControllerHandler)
		{
			var response = new ValidationResponse("The request is invalid.", new Dictionary<string, List<string>>
			{
				{
					validationContext.DisplayName,
					new List<string>
					{
						FormatErrorMessage(validationContext.DisplayName)
					}
				}
			});

			var message = new HttpResponseMessage
			{
				StatusCode = StatusCode,
				ReasonPhrase = FormatErrorMessage(validationContext.DisplayName),
				Content = new ObjectContent<ValidationResponse>(response, new JsonMediaTypeFormatter())
			};

			throw new HttpResponseException(message);
		}

		string[] memberNames = !string.IsNullOrWhiteSpace(validationContext.MemberName) ? [validationContext!.MemberName] : [];

		return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
	}
}