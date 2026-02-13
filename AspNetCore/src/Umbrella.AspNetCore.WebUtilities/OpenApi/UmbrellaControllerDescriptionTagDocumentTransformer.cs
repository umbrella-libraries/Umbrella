#if NET10_0_OR_GREATER
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Transforms an OpenAPI document by enriching tag descriptions using the Description attribute from controller
/// classes.
/// </summary>
/// <remarks>This transformer scans all public, non-abstract controller classes in the assembly that inherit from
/// ControllerBase. For each controller with a Description attribute, it updates the corresponding tag in the OpenAPI
/// document to include the provided description. If no controller descriptions are found, the document remains
/// unmodified. This process helps improve the clarity and usefulness of generated API documentation by providing
/// descriptive information for each controller tag.</remarks>
public sealed class UmbrellaControllerDescriptionTagDocumentTransformer : IOpenApiDocumentTransformer
{
	/// <inheritdoc/>
	public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(document);

		Assembly assembly = typeof(UmbrellaControllerDescriptionTagDocumentTransformer).Assembly;

		IEnumerable<Type> controllerTypes = assembly
			.GetExportedTypes()
			.Where(x => x is { IsClass: true, IsAbstract: false, IsPublic: true })
			.Where(x => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(x));

		Dictionary<string, string?> tagDescriptions = new(StringComparer.OrdinalIgnoreCase);

		foreach (Type controllerType in controllerTypes)
		{
			string? description = controllerType.GetCustomAttribute<DescriptionAttribute>()?.Description;

			if (string.IsNullOrWhiteSpace(description))
				continue;

			string controllerName = controllerType.Name;

			if (controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
				controllerName = controllerName[..^"Controller".Length];

			_ = tagDescriptions.TryAdd(controllerName, description);
		}

		if (tagDescriptions.Count == 0)
			return Task.CompletedTask;

		document.Tags ??= new HashSet<OpenApiTag>();

		foreach (KeyValuePair<string, string?> kvp in tagDescriptions)
		{
			string tagName = kvp.Key;
			string? tagDescription = kvp.Value;

			OpenApiTag? existing = document.Tags.FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));

			if (existing is null)
			{
				_ = document.Tags.Add(new OpenApiTag { Name = tagName, Description = tagDescription });
			}
			else if (string.IsNullOrWhiteSpace(existing.Description))
			{
				existing.Description = tagDescription;
			}
		}

		return Task.CompletedTask;
	}
}
#endif