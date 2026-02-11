


using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for the <see cref="IMvcBuilder"/> type.
/// </summary>
public static class IMvcBuilderExtensions
{
	/// <summary>
	/// Configures custom API behavior options for Umbrella, including a validation problem details response for invalid
	/// model states.
	/// </summary>
	/// <remarks>
	/// This method customizes the response returned when model validation fails, returning a problem details object with a
	/// status code of 400 or 422 depending on the model state. Use this method to ensure consistent validation error
	/// responses across your API.
	/// </remarks>
	/// <param name="builder">The MVC builder to configure. Cannot be null.</param>
	/// <returns>
	/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
	/// </returns>
	public static IMvcBuilder ConfigureUmbrellaApiBehaviorOptions(this IMvcBuilder builder)
	{
		Guard.IsNotNull(builder);

		_ = builder.ConfigureApiBehaviorOptions(options =>
		{
			options.InvalidModelStateResponseFactory = context =>
			{
				int statusCode = context.ModelState.ContainsKey("$") ? 400 : 422;

				var problemDetails = new UmbrellaValidationProblemDetails(context.ModelState)
				{
					Status = statusCode,
					TraceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
				};

				return new ObjectResult(problemDetails)
				{
					ContentTypes = { "application/problem+json" },
					StatusCode = statusCode
				};
			};
		});

		return builder;
	}

	/// <summary>
	/// Configures Umbrella MVC options, including the insertion of Umbrella's custom model binders.
	/// </summary>
	/// <param name="builder">The MVC builder to configure. Cannot be null.</param>
	/// <returns>
	/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
	/// </returns>
	public static IMvcBuilder ConfigureUmbrellaMvcOptions(this IMvcBuilder builder)
	{
		Guard.IsNotNull(builder);

		_ = builder.AddMvcOptions(options =>
		{
			_ = options.InsertUmbrellaModelBinders();
		});

		return builder;
	}

	/// <summary>
	/// Configures Umbrella JSON options.
	/// </summary>
	/// <param name="builder">The MVC builder to configure. Cannot be null.</param>
	/// <param name="isDevelopment">Determines whether the application is running in a development environment.</param>
	/// <returns>
	/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
	/// </returns>
	public static IMvcBuilder ConfigureUmbrellaJsonOptions(this IMvcBuilder builder, bool isDevelopment)
	{
		Guard.IsNotNull(builder);

		_ = builder.AddJsonOptions(options =>
		{
			options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
			options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
			options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
			options.JsonSerializerOptions.WriteIndented = isDevelopment;
		});

		return builder;
	}

	/// <summary>
	/// Configures Umbrella MVC builder options.
	/// </summary>
	/// <param name="builder">The MVC builder to configure. Cannot be null.</param>
	/// <param name="isDevelopment">Determines whether the application is running in a development environment.</param>
	/// <returns>
	/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
	/// </returns>
	/// <remarks>
	/// Internally, this method calls: <list type="bullet"> <item>
	/// <see cref="ConfigureUmbrellaApiBehaviorOptions(IMvcBuilder)"/> </item> <item>
	/// <see cref="ConfigureUmbrellaMvcOptions(IMvcBuilder)"/> </item> <item>
	/// <see cref="ConfigureUmbrellaJsonOptions(IMvcBuilder, bool)"/> </item> </list>
	/// </remarks>
	public static IMvcBuilder ConfigureUmbrellaMvcBuilderOptions(this IMvcBuilder builder, bool isDevelopment)
	{
		Guard.IsNotNull(builder);

		_ = builder.ConfigureUmbrellaApiBehaviorOptions();
		_ = builder.ConfigureUmbrellaMvcOptions();
		_ = builder.ConfigureUmbrellaJsonOptions(isDevelopment);

		return builder;
	}
}