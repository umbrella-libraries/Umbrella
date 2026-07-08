
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbrella.AspNetCore.WebUtilities.Mvc;
using Umbrella.AspNetCore.WebUtilities.Mvc.ModelBinding.Binders;
using Umbrella.AspNetCore.WebUtilities.OpenApi;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for the <see cref="IMvcBuilder"/> type.
/// </summary>
public static class IMvcBuilderExtensions
{
	extension(IMvcBuilder builder)
	{
		/// <summary>
		/// Configures custom API behavior options for Umbrella, including a validation problem details response for invalid
		/// model states.
		/// </summary>
		/// <remarks>
		/// This method customizes the response returned when model validation fails, returning a problem details object with
		/// a status code of 400 or <paramref name="validationFailureStatusCode"/> depending on the model state. Model state
		/// entries keyed by <c>$</c> indicate JSON input-formatting errors at the root of the request body, such as malformed
		/// JSON or a root value that cannot be converted to the action parameter type. These are always treated as bad requests
		/// and return 400. Other model state errors are treated as validation failures against a successfully parsed request
		/// body and return <paramref name="validationFailureStatusCode"/>, which defaults to 422. Use this method to ensure
		/// consistent validation error responses across your API.
		/// </remarks>
		/// <param name="validationFailureStatusCode">
		/// The status code returned for model state errors that are not JSON input-formatting errors at the root of the
		/// request body. Defaults to <see cref="StatusCodes.Status422UnprocessableEntity"/>.
		/// </param>
		/// <returns>
		/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
		/// </returns>
		public IMvcBuilder ConfigureUmbrellaApiBehaviorOptions(int validationFailureStatusCode = StatusCodes.Status422UnprocessableEntity)
		{
			Guard.IsNotNull(builder);

			_ = builder.ConfigureApiBehaviorOptions(options =>
			{
				options.InvalidModelStateResponseFactory = context =>
				{
					int statusCode = context.ModelState.ContainsKey("$") ? StatusCodes.Status400BadRequest : validationFailureStatusCode;

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
		/// <returns>
		/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
		/// </returns>
		public IMvcBuilder ConfigureUmbrellaMvcOptions()
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
		/// <param name="isDevelopment">Determines whether the application is running in a development environment.</param>
		/// <param name="addJsonStringEnumConverter">Determines whether to add a JSON string enum converter.</param>
		/// <returns>
		/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
		/// </returns>
		public IMvcBuilder ConfigureUmbrellaJsonOptions(bool isDevelopment, bool addJsonStringEnumConverter = false)
		{
			Guard.IsNotNull(builder);

			_ = builder.AddJsonOptions(options =>
			{
				options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
				options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
				
				if (addJsonStringEnumConverter)
					options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
				
				options.JsonSerializerOptions.WriteIndented = isDevelopment;
			});

			return builder;
		}

		/// <summary>
		/// Configures OpenAPI conventions for the specified MVC builder to enhance API response type documentation.
		/// </summary>
		/// <remarks>
		/// This method adds a custom response type convention to the MVC options, which helps ensure consistent and accurate
		/// API documentation when using OpenAPI tools.
		/// </remarks>
		/// <returns>The same instance of the <see cref="IMvcBuilder"/>, enabling method chaining.</returns>
		public IMvcBuilder ConfigureUmbrellaOpenApiConventions()
		{
			Guard.IsNotNull(builder);

			_ = builder.AddMvcOptions(options =>
			{
				options.Conventions.Add(new UmbrellaProducesResponseTypeConvention());
			});

			return builder;
		}

		/// <summary>
		/// Configures Umbrella MVC builder options.
		/// </summary>
		/// <param name="isDevelopment">Determines whether the application is running in a development environment.</param>
		/// <param name="addJsonStringEnumConverter">Determines whether to add a JSON string enum converter.</param>
		/// <param name="validationFailureStatusCode">
		/// The status code returned for model state errors that are not JSON input-formatting errors at the root of the
		/// request body. Defaults to <see cref="StatusCodes.Status422UnprocessableEntity"/>.
		/// </param>
		/// <returns>
		/// The same <see cref="IMvcBuilder"/> instance so that additional configuration calls can be chained.
		/// </returns>
		/// <remarks>
		/// Internally, this method calls:
		/// <list type="bullet">
		/// <item>
		/// <see cref="ConfigureUmbrellaApiBehaviorOptions(IMvcBuilder, int)"/>
		/// </item>
		/// <item>
		/// <see cref="ConfigureUmbrellaMvcOptions(IMvcBuilder)"/>
		/// </item>
		/// <item>
		/// <see cref="ConfigureUmbrellaJsonOptions(IMvcBuilder, bool, bool)"/>
		/// </item>
		/// <item>
		/// <see cref="ConfigureUmbrellaOpenApiConventions(IMvcBuilder)"/>
		/// </item>
		/// </list>
		/// </remarks>
		public IMvcBuilder ConfigureUmbrellaMvcBuilderOptions(bool isDevelopment, bool addJsonStringEnumConverter = false, int validationFailureStatusCode = StatusCodes.Status422UnprocessableEntity)
		{
			Guard.IsNotNull(builder);

			_ = builder.ConfigureUmbrellaApiBehaviorOptions(validationFailureStatusCode);
			_ = builder.ConfigureUmbrellaMvcOptions();
			_ = builder.ConfigureUmbrellaJsonOptions(isDevelopment, addJsonStringEnumConverter);
			_ = builder.ConfigureUmbrellaOpenApiConventions();

			return builder;
		}
	}
}
