#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Transforms OpenAPI operations to apply security requirements based on authorization attributes present on the
/// endpoint.
/// </summary>
/// <remarks>This transformer inspects the endpoint's metadata to determine if anonymous access is allowed or if
/// authorization is required. When authorization attributes are present, it extracts the specified authentication
/// schemes and updates the OpenAPI operation's security requirements accordingly. If no authentication schemes are
/// specified, the default authentication scheme is used if available. This ensures that the generated OpenAPI
/// documentation accurately reflects the security expectations for each operation, which is important for client code
/// generation and API consumers.</remarks>
internal sealed class UmbrellaAuthorizeOperationSecurityTransformer : IOpenApiOperationTransformer
{
	/// <inheritdoc/>
	public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (IsAnonymous(context))
		{
			// Ensure explicit anonymous marker in the OpenAPI document.
			// Some UIs treat missing `security` as inheriting global security.
			operation.Security ??= [];
			operation.Security.Clear();

			return;
		}

		AuthorizeAttribute[] authorizeAttributes = GetAuthorizeAttributes(context);

		if (authorizeAttributes.Length is 0)
			return;

		IServiceProvider services = context.ApplicationServices;

		var defaultAuthenticationScheme = await services.GetRequiredService<IAuthenticationSchemeProvider>().GetDefaultAuthenticateSchemeAsync();

		string? defaultScheme = defaultAuthenticationScheme?.Name;

		HashSet<string> schemes = new(StringComparer.OrdinalIgnoreCase);

		foreach (AuthorizeAttribute attribute in authorizeAttributes)
		{
			if (string.IsNullOrWhiteSpace(attribute.AuthenticationSchemes))
				continue;

			foreach (string scheme in attribute.AuthenticationSchemes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				_ = schemes.Add(scheme);
		}

		if (schemes.Count is 0)
		{
			if (!string.IsNullOrWhiteSpace(defaultScheme))
				_ = schemes.Add(defaultScheme);
		}

		if (schemes.Count is 0)
			return;

		operation.Security ??= [];
		operation.Security.Clear();

		foreach (string scheme in schemes)
		{
			// Use a scheme reference so the JSON becomes `{ "Bearer": [] }` etc.
			string securitySchemeReferenceId = scheme;

			OpenApiSecuritySchemeReference securitySchemeReference = new(securitySchemeReferenceId)
			{
				Description = $"Authentication scheme: {securitySchemeReferenceId}",
				Reference = new OpenApiReferenceWithDescriptionAndSummary
				{
					Type = ReferenceType.SecurityScheme,
					Id = securitySchemeReferenceId,
					Summary = $"Authentication scheme: {securitySchemeReferenceId}",
					HostDocument = context.Document,
				},
			};
			operation.Security.Add(new OpenApiSecurityRequirement
			{
				[securitySchemeReference] = [],
			});
		}

		return;
	}

	private static bool IsAnonymous(OpenApiOperationTransformerContext context)
	{
		IEnumerable<object> endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;

		return endpointMetadata.Any(x => x is IAllowAnonymous);
	}

	private static AuthorizeAttribute[] GetAuthorizeAttributes(OpenApiOperationTransformerContext context)
	{
		IEnumerable<object> endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;

		return [.. endpointMetadata.OfType<AuthorizeAttribute>()];
	}
}
#endif