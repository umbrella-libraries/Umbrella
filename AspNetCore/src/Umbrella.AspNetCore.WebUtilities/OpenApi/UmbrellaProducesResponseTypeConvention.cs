using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbrella.AspNetCore.WebUtilities.Mvc;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// An application model convention that ensures only one <see cref="UmbrellaProducesResponseTypeAttribute"/> (generic
/// or non-generic) is applied per status code for each action.
/// </summary>
public sealed class UmbrellaProducesResponseTypeConvention : IApplicationModelConvention
{
	/// <inheritdoc/>
	public void Apply(ApplicationModel application)
	{
		Guard.IsNotNull(application);

		foreach (var controller in application.Controllers)
		{
			foreach (var action in controller.Actions)
			{
				// Find all Umbrella ProducesResponseType attributes (generic and non-generic) present as filters
				var responseTypeFilters = action.Filters
					.Where(f => f is UmbrellaProducesResponseTypeAttribute || IsGenericUmbrellaProducesResponseTypeFilter(f))
					.Cast<IFilterMetadata>()
					.ToList();

				// Group by status code
				var groupedFilters = responseTypeFilters.GroupBy(GetStatusCodeFromFilter);

				foreach (var group in groupedFilters)
				{
					if (group.Count() > 1)
					{
						// Attributes declared directly on derived method (inherit: false)
						var derivedAttrs = action.ActionMethod
							.GetCustomAttributes(inherit: false)
							.Where(a => a is UmbrellaProducesResponseTypeAttribute || IsGenericUmbrellaProducesResponseTypeFilter(a))
							.Cast<IFilterMetadata>()
							.ToList();

						// Pick the derived attribute matching the group's status code
						var winningAttribute = derivedAttrs.FirstOrDefault(a => GetStatusCodeFromFilter(a) == group.Key);

						// Remove all attributes for this status code from the action's filters
						foreach (var remove in group.ToList())
						{
							_ = action.Filters.Remove(remove);
						}

						// Add back the derived attribute if present; otherwise, keep the last one as a fallback
						if (winningAttribute is not null)
						{
							action.Filters.Add(winningAttribute);
						}
						else
						{
							var fallback = group.LastOrDefault();

							if (fallback is not null)
								action.Filters.Add(fallback);
						}
					}
				}
			}
		}
	}

	private static bool IsGenericUmbrellaProducesResponseTypeFilter(object f)
	{
		var type = f.GetType();

		return type.IsGenericType && type.GetGenericTypeDefinition().Name is nameof(UmbrellaProducesResponseTypeAttribute) + "`1";
	}

	private static int GetStatusCodeFromFilter(IFilterMetadata filter)
	{
		PropertyInfo? piStatusCode = filter.GetType().GetProperty(nameof(UmbrellaProducesResponseTypeAttribute.StatusCode)) ?? throw new UnreachableException("The filter does not have a StatusCode property.");

		object objStatusCode = piStatusCode.GetValue(filter) ?? throw new UnreachableException("The StatusCode property is null.");

		return objStatusCode is int statusCode ? statusCode : throw new UnreachableException("The StatusCode property is not of type int.");
	}
}