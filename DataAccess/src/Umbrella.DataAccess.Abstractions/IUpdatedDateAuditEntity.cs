


namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// Adds support to an entity type for storing the date it was last updated.
/// </summary>
public interface IUpdatedDateAuditEntity
{
	/// <summary>
	/// Gets or sets the date the entity was last updated.
	/// </summary>
	DateTime UpdatedDateUtc { get; set; }
}