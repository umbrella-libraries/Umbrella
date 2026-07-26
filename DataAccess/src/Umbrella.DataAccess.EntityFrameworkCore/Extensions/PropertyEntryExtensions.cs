using CommunityToolkit.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Umbrella.DataAccess.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for the <see cref="PropertyEntry{TEntity, TProperty}" /> class.
/// </summary>
public static class PropertyEntryExtensions
{
	/// <summary>
	/// Checks for changes to the value of a property that is being tracked by a <see cref="DbContext"/>.
	/// </summary>
	/// <param name="property">The property to check for changes.</param>
	/// <returns><see langword="true"/> if the value of the <paramref name="property"/> has changed; otherwise <see langword="false"/></returns>
	/// <remarks>This method will return <see langword="true"/> when the property has been modified from the default value for the property's type
	/// even when the entity is new which is <b>NOT</b> the case when checking for modifications using the <see cref="PropertyEntry.IsModified"/> property.
	/// This will always return <see langword="false"/> for new entities.
	/// </remarks>
	public static bool HasChanged(this PropertyEntry property)
	{
		Guard.IsNotNull(property);

		object? currentValue = property.CurrentValue;
		object? originalValue = property.OriginalValue;

		if (currentValue is null && originalValue is null)
			return false;

		if (currentValue is null && originalValue is not null)
			return true;

		if (currentValue is not null && originalValue is null)
			return true;

		return !currentValue!.Equals(originalValue);
	}
}
