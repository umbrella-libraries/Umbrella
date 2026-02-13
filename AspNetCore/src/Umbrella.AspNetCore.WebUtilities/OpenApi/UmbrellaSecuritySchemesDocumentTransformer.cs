#if NET10_0_OR_GREATER
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Transforms an OpenAPI document by adding security schemes based on the authentication schemes registered in the
/// application.
/// </summary>
/// <remarks>This transformer retrieves all authentication schemes and adds them to the OpenAPI document's
/// components. It also sets a global default security requirement for the document, allowing UIs to indicate secured
/// operations.
/// </remarks>
public sealed class UmbrellaSecuritySchemesDocumentTransformer : IOpenApiDocumentTransformer
{
	/// <inheritdoc/>
	public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(document);
		Guard.IsNotNull(context);

		IServiceProvider services = context.ApplicationServices;
		IAuthenticationSchemeProvider schemeProvider = services.GetRequiredService<IAuthenticationSchemeProvider>();

		IEnumerable<AuthenticationScheme> schemes = await schemeProvider.GetAllSchemesAsync().ConfigureAwait(false);

		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.OrdinalIgnoreCase);

		foreach (AuthenticationScheme scheme in schemes)
		{
			if (document.Components.SecuritySchemes.ContainsKey(scheme.Name))
				continue;

			OpenApiSecurityScheme? openApiScheme = CreateScheme(scheme);

			if (openApiScheme is null)
				continue;

			document.Components.SecuritySchemes.Add(scheme.Name, openApiScheme);
		}

		// Provide a global default `security` requirement so UIs (e.g. Scalar) can
		// render a lock icon for secured operations. `[AllowAnonymous]` operations
		// can override this by having an explicit empty `security` array.
		var defaultAuthenticateScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync().ConfigureAwait(false);
		string? defaultSchemeName = defaultAuthenticateScheme?.Name;
		if (!string.IsNullOrWhiteSpace(defaultSchemeName))
		{
			document.Security ??= [];
			document.Security.Clear();
			document.Security.Add(new OpenApiSecurityRequirement
			{
				[new OpenApiSecuritySchemeReference(defaultSchemeName)] = [],
			});
		}
	}

	private static OpenApiSecurityScheme? CreateScheme(AuthenticationScheme scheme)
	{
		return scheme.HandlerType == typeof(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler)
			? new OpenApiSecurityScheme
			{
				Name = "Authorization",
				Type = SecuritySchemeType.Http,
				Scheme = "bearer",
				In = ParameterLocation.Header,
				BearerFormat = "JWT",
				Description = "JWT Bearer token. Use the Authorization header: `Authorization: Bearer {token}`.",
			}
			: new OpenApiSecurityScheme
			{
				// Our custom schemes use the Authorization header in the form:
				// Authorization: {SchemeName} {ApiKey}
				Name = "Authorization",
				Type = SecuritySchemeType.Http,
				Scheme = scheme.Name,
				In = ParameterLocation.Header,
				BearerFormat = "API Key",
				Description = $"API key authentication. Use the Authorization header: `Authorization: {scheme.Name} {{api-key}}`.",
			};
	}
}
#endif