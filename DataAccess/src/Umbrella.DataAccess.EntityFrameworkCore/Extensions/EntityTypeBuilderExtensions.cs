


using CommunityToolkit.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Umbrella.DataAccess.Abstractions;
using Umbrella.Utilities.Data.Concurrency;

namespace Umbrella.DataAccess.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for the <see cref="EntityTypeBuilder{T}" /> class.
/// </summary>
public static class EntityTypeBuilderExtensions
{
	/// <summary>
	/// Setups the concurrency token.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder.</returns>
	public static PropertyBuilder<string> SetupConcurrencyToken<TEntity>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class, IConcurrencyStamp
	{
		Guard.IsNotNull(builder);

		return builder.Property(x => x.ConcurrencyStamp).HasMaxLength(36).IsRequired().IsConcurrencyToken();
	}

	/// <summary>
	/// Setups the <see cref="ICreatedDateAuditEntity.CreatedDateUtc"/> audit property and ensure that values are converted to and from UTC when storing and retrieving.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder.</returns>
	public static PropertyBuilder<DateTime> SetupCreatedDateUtcAuditProperty<TEntity>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class, ICreatedDateAuditEntity
	{
		Guard.IsNotNull(builder);

		return builder.Property(x => x.CreatedDateUtc).EnsureUtc();
	}

	/// <summary>
	/// Setups the <see cref="IUpdatedDateAuditEntity.UpdatedDateUtc"/> audit property and ensure that values are converted to and from UTC when storing and retrieving.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder.</returns>
	public static PropertyBuilder<DateTime> SetupUpdatedDateUtcAuditProperty<TEntity>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class, IUpdatedDateAuditEntity
	{
		Guard.IsNotNull(builder);

		return builder.Property(x => x.UpdatedDateUtc).EnsureUtc();
	}

	/// <summary>
	/// Setups the <see cref="ICreatedUserAuditEntity{TUserId}.CreatedById"/> audit property including foreign key relationships to the <typeparamref name="TAppUserEntity"/>
	/// as well as behaviour applied when deleting entities.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TAppUserEntity">The type of the application user entity.</typeparam>
	/// <typeparam name="TUserId">The type of the user identifier.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder.</returns>
	public static ReferenceCollectionBuilder<TAppUserEntity, TEntity> SetupCreatedByIdAuditProperty<TEntity, TAppUserEntity, TUserId>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class, ICreatedUserAuditEntity<TUserId>
		where TAppUserEntity : class
	{
		Guard.IsNotNull(builder);

		return builder.HasOne<TAppUserEntity>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
	}

	/// <summary>
	/// Setups the <see cref="IUpdatedUserAuditEntity{TUserId}.UpdatedById"/> audit property including foreign key relationships to the <typeparamref name="TAppUserEntity"/>
	/// as well as behaviour applied when deleting entities.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TAppUserEntity">The type of the application user entity.</typeparam>
	/// <typeparam name="TUserId">The type of the user identifier.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder.</returns>
	public static ReferenceCollectionBuilder<TAppUserEntity, TEntity> SetupUpdatedByIdAuditProperty<TEntity, TAppUserEntity, TUserId>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class, IUpdatedUserAuditEntity<TUserId>
		where TAppUserEntity : class
	{
		Guard.IsNotNull(builder);

		return builder.HasOne<TAppUserEntity>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
	}

	/// <summary>
	/// Applies the setup to all audit properties specified on the <see cref="IAuditEntity{TEntityKey, TAuditKey}"/> interface.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TAppUserEntity">The type of the application user entity.</typeparam>
	/// <typeparam name="TUserId">The type of the user identifier.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The builder.</returns>
	/// <remarks>
	/// Internally, this method calls into the following:
	/// <list type="bullet">
	/// <item><see cref="SetupCreatedDateUtcAuditProperty{TEntity}(EntityTypeBuilder{TEntity})"/></item>
	/// <item><see cref="SetupUpdatedDateUtcAuditProperty{TEntity}(EntityTypeBuilder{TEntity})"/></item>
	/// <item><see cref="SetupCreatedByIdAuditProperty{TEntity, TAppUserEntity, TUserId}(EntityTypeBuilder{TEntity})"/></item>
	/// <item><see cref="SetupUpdatedByIdAuditProperty{TEntity, TAppUserEntity, TUserId}(EntityTypeBuilder{TEntity})"/></item>
	/// </list>
	/// </remarks>
	public static EntityTypeBuilder<TEntity> SetupAuditProperties<TEntity, TAppUserEntity, TUserId>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class, ICreatedDateAuditEntity, IUpdatedDateAuditEntity, ICreatedUserAuditEntity<TUserId>, IUpdatedUserAuditEntity<TUserId>
		where TAppUserEntity : class
	{
		_ = SetupCreatedDateUtcAuditProperty(builder);
		_ = SetupUpdatedDateUtcAuditProperty(builder);
		_ = SetupCreatedByIdAuditProperty<TEntity, TAppUserEntity, TUserId>(builder);
		_ = SetupUpdatedByIdAuditProperty<TEntity, TAppUserEntity, TUserId>(builder);

		return builder;
	}
}