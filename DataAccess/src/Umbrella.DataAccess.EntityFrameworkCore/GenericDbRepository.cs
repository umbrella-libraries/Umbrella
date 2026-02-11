


using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.Abstractions.Exceptions;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Concurrency;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.Dating.Abstractions;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.DataAccess.EntityFrameworkCore;

// TODO: Improve the documentation, especially around method lifecycles and the virtual methods.

/// <summary>
/// Serves as the base class for repositories which provide CRUD access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public abstract class GenericDbRepository<TEntity, TDbContext> : GenericDbRepository<TEntity, TDbContext, RepoOptions>
	where TEntity : class, IEntity<int>
	where TDbContext : DbContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GenericDbRepository{TEntity, TDbContext}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	/// <param name="dbContextHelper">The database context helper.</param>
	/// <param name="entityValidator">The entity validator.</param>
	/// <param name="dateTimeProvider"></param>
	protected GenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer,
		IUmbrellaDbContextHelper dbContextHelper,
		IEntityValidator entityValidator,
		IDateTimeProvider dateTimeProvider)
		: base(dbContext, logger, lookupNormalizer, dbContextHelper, entityValidator, dateTimeProvider)
	{
	}
}

/// <summary>
/// Serves as the base class for repositories which provide CRUD access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
public abstract class GenericDbRepository<TEntity, TDbContext, TRepoOptions> : GenericDbRepository<TEntity, TDbContext, TRepoOptions, int>
	where TEntity : class, IEntity<int>
	where TDbContext : DbContext
	where TRepoOptions : RepoOptions, new()
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GenericDbRepository{TEntity, TDbContext, TRepoOptions}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	/// <param name="dbContextHelper">The database context helper.</param>
	/// <param name="entityValidator">The entity validator.</param>
	/// <param name="dateTimeProvider"></param>
	protected GenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer,
		IUmbrellaDbContextHelper dbContextHelper,
		IEntityValidator entityValidator,
		IDateTimeProvider dateTimeProvider)
		: base(dbContext, logger, lookupNormalizer, dbContextHelper, entityValidator, dateTimeProvider)
	{
	}
}

/// <summary>
/// Serves as the base class for repositories which provide CRUD access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
public abstract class GenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey> : GenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, int>
	where TEntity : class, IEntity<TEntityKey>
	where TDbContext : DbContext
	where TRepoOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	/// <param name="dbContextHelper">The database context helper.</param>
	/// <param name="entityValidator">The entity validator.</param>
	/// <param name="dateTimeProvider"></param>
	protected GenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer,
		IUmbrellaDbContextHelper dbContextHelper,
		IEntityValidator entityValidator,
		IDateTimeProvider dateTimeProvider)
		: base(dbContext, logger, lookupNormalizer, dbContextHelper, entityValidator, dateTimeProvider)
	{
	}
}

/// <summary>
/// Serves as the base class for repositories which provide CRUD access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
/// <typeparam name="TUserAuditKey">The type of the user audit key.</typeparam>
public abstract class GenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey> : ReadOnlyGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey>, IGenericDbRepository<TEntity, TRepoOptions, TEntityKey>
	where TEntity : class, IEntity<TEntityKey>
	where TDbContext : DbContext
	where TRepoOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
{
	#region Protected Properties		
	/// <summary>
	/// Gets the database context helper.
	/// </summary>
	protected IUmbrellaDbContextHelper DbContextHelper { get; }

	/// <summary>
	/// Gets the entity validator.
	/// </summary>
	protected IEntityValidator EntityValidator { get; }

	/// <summary>
	/// Gets the date time provider.
	/// </summary>
	protected IDateTimeProvider DateTimeProvider { get; }
	#endregion

	#region Constructors			
	/// <summary>
	/// Initializes a new instance of the <see cref="GenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	/// <param name="dbContextHelper">The database context helper.</param>
	/// <param name="entityValidator">The entity validator.</param>
	/// <param name="dateTimeProvider">The date time provider.</param>
	protected GenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer,
		IUmbrellaDbContextHelper dbContextHelper,
		IEntityValidator entityValidator,
		IDateTimeProvider dateTimeProvider)
		: base(dbContext, logger, lookupNormalizer)
	{
		DbContextHelper = dbContextHelper;
		EntityValidator = entityValidator;
		DateTimeProvider = dateTimeProvider;
	}
	#endregion

	#region Save
	/// <inheritdoc />
	[Obsolete($"Use the {nameof(SaveEntityAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	public virtual async Task<IOperationResult<TEntity>> SaveAsync(TEntity entity, bool pushChangesToDb = true, bool addToContext = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, bool forceAdd = false, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(entity);

		try
		{
			repoOptions ??= DefaultRepoOptions;

			if (repoOptions.ThrowIfConcurrencyTokenMismatch)
				ThrowIfConcurrencyTokenMismatch(entity);

			if (repoOptions.SanitizeEntity)
				await SanitizeEntityAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

			// Look for the entity in the context - this action will allow us to determine its state
			EntityEntry<TEntity> dbEntity = Context.Value.Entry(entity);

			bool isNew = forceAdd || (entity.Id.Equals(default!) && (dbEntity.State.HasFlag(EntityState.Added) || dbEntity.State.HasFlag(EntityState.Detached)));

			// Additional processing before changes have been reflected in the database context
			await BeforeContextSavingAsync(entity, repoOptions, childOptions, isNew, cancellationToken).ConfigureAwait(false);

			if (repoOptions.ValidateEntity)
			{
				ICollection<ValidationResult> lstValidationResult = await ValidateEntityAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

				if (lstValidationResult.Count > 0)
					return OperationResult<TEntity>.GenericFailure(lstValidationResult, entity);
			}

			bool entityHasChanged = false;

			if (isNew)
			{
				entityHasChanged = true;

				if (entity is ICreatedDateAuditEntity dateAuditEntity)
					dateAuditEntity.CreatedDateUtc = DateTimeProvider.UtcNow;

				if (entity is ICreatedUserAuditEntity<TUserAuditKey> userAuditEntity)
					userAuditEntity.CreatedById = CurrentUserId ?? default!;

				if (addToContext)
					_ = Context.Value.Set<TEntity>().Add(entity);
			}

			if (repoOptions.UpdateOriginalConcurrencyStamp)
				UpdateOriginalConcurrencyStamp(entity);

			if (dbEntity.State.HasFlag(EntityState.Added) || dbEntity.State.HasFlag(EntityState.Detached) || dbEntity.State.HasFlag(EntityState.Modified))
			{
				entityHasChanged = true;

				if (entity is IUpdatedDateAuditEntity dateAuditEntity)
					dateAuditEntity.UpdatedDateUtc = DateTimeProvider.UtcNow;

				if (entity is IUpdatedUserAuditEntity<TUserAuditKey> userAuditEntity)
					userAuditEntity.UpdatedById = CurrentUserId ?? default!;
			}

			if (entityHasChanged)
			{
				// Set the Concurrency Stamp
				if (entity is IConcurrencyStamp concurrencyStampEntity)
					concurrencyStampEntity.UpdateConcurrencyStamp();
			}

			// Additional processing after changes have been reflected in the database context but not yet pushed to the database
			await AfterContextSavingAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

			DbContextHelper.RegisterPostSaveChangesAction(entity, (cancellationToken) => AfterContextSavedChangesAsync(entity, isNew, repoOptions, childOptions, cancellationToken));

			if (pushChangesToDb)
				_ = await Context.Value.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			return isNew ? OperationResult<TEntity>.Created(entity) : OperationResult<TEntity>.Success(entity);
		}
		catch (DbUpdateConcurrencyException exc) when (Logger.WriteError(exc, new { entity.Id, pushChangesToDb, addToContext, repoOptions, childOptions }, "Concurrency Exception for Id"))
		{
			throw new UmbrellaConcurrencyException(string.Format(CultureInfo.CurrentCulture, ErrorMessages.ConcurrencyExceptionErrorMessageFormat, entity.Id), exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { entity.Id, pushChangesToDb, addToContext, repoOptions, childOptions }, "Failed for Id", returnValue: exc is not UmbrellaConcurrencyException))
		{
			throw new UmbrellaDataAccessException("There was a problem saving the entity.", exc);
		}
	}

	/// <inheritdoc />
	public virtual Task<IOperationResult<TEntity>> SaveEntityAsync(TEntity entity, bool addToContext = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, bool forceAdd = false, CancellationToken cancellationToken = default)
#pragma warning disable CS0618 // Type or member is obsolete
		=> SaveAsync(entity, false, addToContext, repoOptions, childOptions, forceAdd, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

	/// <inheritdoc />
	[Obsolete($"Use the {nameof(SaveAllEntitiesAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	public virtual async Task<IReadOnlyCollection<IOperationResult<TEntity>>> SaveAllAsync(IEnumerable<TEntity> entities, bool pushChangesToDb = true, bool bypassSaveLogic = false, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(entities);

		try
		{
			// Save all changes - do not push to the database yet
			List<IOperationResult<TEntity>>? lstSaveResult = null;

			if (!bypassSaveLogic)
			{
				lstSaveResult = [];

				foreach (TEntity entity in entities)
				{
					IOperationResult<TEntity> saveResult = await SaveAsync(entity, false, true, repoOptions, childOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
					lstSaveResult.Add(saveResult);
				}
			}

			if (pushChangesToDb)
				_ = await Context.Value.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			return lstSaveResult is not null ? lstSaveResult : entities.Select(OperationResult<TEntity>.Success).ToArray();
		}
		catch (DbUpdateConcurrencyException exc) when (Logger.WriteError(exc, new { ids = FormatEntityIds(entities), pushChangesToDb, bypassSaveLogic, repoOptions, childOptions }, "Bulk Save Concurrency Exception"))
		{
			throw new UmbrellaConcurrencyException(ErrorMessages.BulkActionConcurrencyExceptionErrorMessage, exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { ids = FormatEntityIds(entities), pushChangesToDb, bypassSaveLogic, repoOptions, childOptions }, returnValue: exc is not UmbrellaConcurrencyException))
		{
			throw new UmbrellaDataAccessException("There has been a problem saving the specified entities.", exc);
		}
	}

	/// <inheritdoc/>
	public virtual Task<IReadOnlyCollection<IOperationResult<TEntity>>> SaveAllEntitiesAsync(IEnumerable<TEntity> entities, bool bypassSaveLogic = false, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
#pragma warning disable CS0618 // Type or member is obsolete
		=> SaveAllAsync(entities, false, bypassSaveLogic, repoOptions, childOptions, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
	#endregion

	#region Delete

	/// <inheritdoc />
	[Obsolete($"Use the {nameof(DeleteEntityAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	public virtual async Task DeleteAsync(TEntity entity, bool pushChangesToDb = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(entity);

		try
		{
			repoOptions ??= DefaultRepoOptions;

			if (repoOptions.ThrowIfConcurrencyTokenMismatch)
				ThrowIfConcurrencyTokenMismatch(entity);

			await BeforeContextDeletingAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

			_ = Context.Value.Set<TEntity>().Remove(entity);

			// TODO: Is this line redundant?
			Context.Value.Entry(entity).State = EntityState.Deleted;

			if (repoOptions.UpdateOriginalConcurrencyStamp)
				UpdateOriginalConcurrencyStamp(entity);

			await AfterContextDeletingAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

			DbContextHelper.RegisterPostSaveChangesAction(entity, (cancellationToken) => AfterContextDeletedChangesAsync(entity, repoOptions, childOptions, cancellationToken));

			if (pushChangesToDb)
				_ = await Context.Value.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (DbUpdateConcurrencyException exc) when (Logger.WriteError(exc, new { entity.Id, pushChangesToDb, repoOptions, childOptions }, "Concurrency Exception for Id"))
		{
			throw new UmbrellaConcurrencyException(string.Format(CultureInfo.CurrentCulture, ErrorMessages.ConcurrencyExceptionErrorMessageFormat, entity.Id), exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { entity.Id, pushChangesToDb, repoOptions, childOptions }, "Failed for Id", returnValue: exc is not UmbrellaConcurrencyException))
		{
			throw new UmbrellaDataAccessException("There has been a problem deleting the specified entity.", exc);
		}
	}

	/// <inheritdoc />
	public virtual Task DeleteEntityAsync(TEntity entity, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
#pragma warning disable CS0618 // Type or member is obsolete
		=> DeleteAsync(entity, false, repoOptions, childOptions, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

#if NET9_0_OR_GREATER
	/// <inheritdoc />
	public virtual async Task DeleteEntityByIdAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			_ = await Items.Where(x => x.Id.Equals(id)).ExecuteDeleteAsync(cancellationToken);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }))
		{
			throw new UmbrellaDataAccessException("There has been a problem deleting the specified entity.", exc);
		}
	}
#endif

	/// <inheritdoc />
	[Obsolete($"Use the {nameof(DeleteAllEntitiesAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	public virtual async Task DeleteAllAsync(IEnumerable<TEntity> entities, bool pushChangesToDb = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(entities);

		try
		{
			foreach (TEntity entity in entities)
			{
				await DeleteAsync(entity, false, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);
			}

			if (pushChangesToDb)
				_ = await Context.Value.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (DbUpdateConcurrencyException exc) when (Logger.WriteError(exc, new { ids = FormatEntityIds(entities), pushChangesToDb, repoOptions, childOptions }, "Bulk Delete Concurrency Exception"))
		{
			throw new UmbrellaConcurrencyException(ErrorMessages.BulkActionConcurrencyExceptionErrorMessage, exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { ids = FormatEntityIds(entities), pushChangesToDb, repoOptions, childOptions }, returnValue: exc is not UmbrellaConcurrencyException))
		{
			throw new UmbrellaDataAccessException("There has been a problem deleting the specified entities.", exc);
		}
	}

	/// <inheritdoc/>
	public virtual Task DeleteAllEntitiesAsync(IEnumerable<TEntity> entities, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
#pragma warning disable CS0618 // Type or member is obsolete
		=> DeleteAllAsync(entities, false, repoOptions, childOptions, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

	#endregion

	#region Protected Methods		
	/// <summary>
	/// Determines whether the concurrency token assigned on the entity matches the one stored on the tracking entry in the database context.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <returns>
	///   <see langword="true"/> if it matches; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// When an entity is saved, the value of the <see cref="IConcurrencyStamp.ConcurrencyStamp"/> on the original entity tracked by the <see cref="DbContext"/> is compared against the value loaded
	/// from the database during <see cref="DbContext.SaveChanges()"/> and a <see cref="DbUpdateConcurrencyException"/> is thrown. However, this does not catch cases where the value has
	/// been modified on the entity we are modifying and results in a mismatch with the value of the original entity. This is the purpose of calls to this method.
	/// </para>
	/// <para>
	/// A common use case for this would be where the entity has been loaded by a web app with the ConcurrencyStamp stored in a hidden field which is then POSTed back to the server.
	/// This check allows for differences between these token values to be identified.
	/// </para>
	/// </remarks>
	protected bool IsConcurrencyTokenMismatch(TEntity entity)
	{
		Guard.IsNotNull(entity);

		return !entity.Id.Equals(default!) && entity is IConcurrencyStamp concurrencyStampEntity && Context.Value.Entry(concurrencyStampEntity).Property(x => x.ConcurrencyStamp).OriginalValue != concurrencyStampEntity.ConcurrencyStamp;
	}

	/// <summary>
	/// Throws an exception if there is a concurrency token mismatch.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <remarks>
	/// <para>
	/// When an entity is saved, the value of the <see cref="IConcurrencyStamp.ConcurrencyStamp"/> on the original entity tracked by the <see cref="DbContext"/> is compared against the value loaded
	/// from the database during <see cref="DbContext.SaveChanges()"/> and a <see cref="DbUpdateConcurrencyException"/> is thrown. However, this does not catch cases where the value has
	/// been modified on the entity we are modifying and results in a mismatch with the value of the original entity. This is the purpose of calls to this method.
	/// </para>
	/// <para>
	/// A common use case for this would be where the entity has been loaded by a web app with the ConcurrencyStamp stored in a hidden field which is then POSTed back to the server.
	/// This check allows for differences between these token values to be identified.
	/// </para>
	/// </remarks>
	protected void ThrowIfConcurrencyTokenMismatch(TEntity entity)
	{
		if (IsConcurrencyTokenMismatch(entity))
			throw new UmbrellaConcurrencyException(string.Format(CultureInfo.CurrentCulture, ErrorMessages.ConcurrencyExceptionErrorMessageFormat, entity.Id));
	}

	/// <summary>
	/// Formats the entity ids as comma-delimited string. This is only used for logging purposes to record the ids.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <returns>A comma-delimited string of entity ids.</returns>
	protected virtual string FormatEntityIds(IEnumerable<TEntity> entities) => string.Join(",", entities.Select(x => x.Id));

	#region Events		
	/// <summary>
	/// Overriding this method allows you to perform custom validation on the entity before its state on the database context is affected.
	/// By default, this calls into the <see cref="Validator.TryValidateObject(object, ValidationContext, ICollection{ValidationResult}, bool)"/> method.
	/// By design, this doesn't recursively perform validation on the entity. If this is required, override this method and use the <see cref="IObjectGraphValidator"/>
	/// by injecting it as a service or perform more extensive validation elsewhere.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task<ICollection<ValidationResult>> ValidateEntityAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		ICollection<ValidationResult> lstValidationResult = [];

		var ctx = new ValidationContext(entity);

		_ = Validator.TryValidateObject(entity, ctx, lstValidationResult, true);

		return Task.FromResult(lstValidationResult);
	}

	/// <summary>
	/// Overriding this method allows you to perform work before the state of the entity on the database context is affected.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="isNew">Specifies whether the <paramref name="entity"/> is new.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task BeforeContextSavingAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, bool isNew, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform work after the state of the entity on the database context has been affected but before
	/// the changes have been pushed to the database.
	/// </summary>
	/// <param name="entity">The entity</param>
	/// <param name="options">The options. If not overridden with a different generic type parameter, the default of <see cref="RepoOptions"/> is used. This parameter will never be null.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task AfterContextSavingAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform any work after the call to <see cref="T:UmbrellaDbContext.SaveChangesAsync()"/> has taken place.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="isNew">Specifies if the entity is new.</param>
	/// <param name="options">The options. If not overridden with a different generic type parameter, the default of <see cref="RepoOptions"/> is used. This parameter will never be null.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task AfterContextSavedChangesAsync(TEntity entity, bool isNew, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform work before the state of the entity on the database context is affected.
	/// </summary>
	/// <param name="entity">The entity</param>
	/// <param name="options">The options. If not overridden with a different generic type parameter, the default of <see cref="RepoOptions"/> is used. This parameter will never be null.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task BeforeContextDeletingAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform work after the state of the entity on the database context is affected.
	/// </summary>
	/// <param name="entity">The entity</param>
	/// <param name="options">The options. If not overridden with a different generic type parameter, the default of <see cref="RepoOptions"/> is used. This parameter will never be null.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task AfterContextDeletingAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform any work after the call to either <see cref="T:UmbrellaDbContext.SaveChangesAsync"/> or <see cref="T:UmbrellaDbContext.SaveChangesAsync(CancellationToken)"/> has taken place.
	/// </summary>
	/// <param name="entity">The entity</param>
	/// <param name="options">The options. If not overridden with a different generic type parameter, the default of <see cref="RepoOptions"/> is used. This parameter will never be null.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected virtual Task AfterContextDeletedChangesAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}
	#endregion

	#region SyncDependencies
	/// <summary>
	/// Synchronizes the dependencies of the current entity.
	/// For example, the entity may have a collection property, e.g. Person has a list of Pets and Pet has a foreign key of PersonId so
	/// there is a 1-N relationship between Person and Pet. This method would be called as: SyncDependenciesAsync(person.Pets, petRepo, x => x.PersonId == person.Id, cancellationToken, options).
	/// <para>This method should be used with caution as it has the overhead of loading all of the child entities that match the predicate, e.g. all Pets for the person.Id. For large collections this is not practical so use with care as alternative solutions will likely be more performant.</para>
	/// </summary>
	/// <typeparam name="TTargetEntity">The type of the target entity.</typeparam>
	/// <typeparam name="TTargetEntityRepoOptions">The type of the target entity repo options.</typeparam>
	/// <typeparam name="TTargetEntityKey">The type of the target entity key.</typeparam>
	/// <typeparam name="TTargetRepository">The type of the target repository.</typeparam>
	/// <param name="alteredColl">The altered collection.</param>
	/// <param name="repository">The target repository.</param>
	/// <param name="predicate">The predicate used to filter .</param>
	/// <param name="repoOptions">The collection of repo options. This can be used by this method to determine if the synchronization logic should be executed.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task" /> which will complete once all work has been completed.</returns>
	protected virtual async Task SyncDependenciesAsync<TTargetEntity, TTargetEntityKey, TTargetEntityRepoOptions, TTargetRepository>(ICollection<TTargetEntity> alteredColl, TTargetRepository repository, Expression<Func<TTargetEntity, bool>> predicate, IEnumerable<RepoOptions>? repoOptions, CancellationToken cancellationToken)
		where TTargetEntity : class, IEntity<TTargetEntityKey>
		where TTargetEntityKey : IEquatable<TTargetEntityKey>
		where TTargetEntityRepoOptions : RepoOptions, new()
		where TTargetRepository : IGenericDbRepository<TTargetEntity, TTargetEntityRepoOptions, TTargetEntityKey>
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Find the RepoOptions for this repository if provided in the options collection
		TTargetEntityRepoOptions? targetOptions = repoOptions?.OfType<TTargetEntityRepoOptions>().FirstOrDefault();

		// Copy the incoming list here - this is because the code in foreach declaration below finds all the entities matching the where clause
		// but the problem is that when that happens, the alteredColl parameter is a reference to the same underlying collection. This means
		// any items that have been removed from the incoming alteredColl will be added back to it. To get around this, we need to copy all the items from alteredColl
		// to a new List first to stop this from happening.
		alteredColl = [.. alteredColl];

		// Ensure we have deleted the dependencies (children) we no longer need
		foreach (TTargetEntity entity in Context.Value.Set<TTargetEntity>().Where(predicate))
		{
			if (!alteredColl.Contains(entity))
			{
				// Delete the dependency, but do not push changes to the database
				await repository.DeleteAsync(entity, false, targetOptions, repoOptions, cancellationToken).ConfigureAwait(false);
			}
		}

		foreach (TTargetEntity entity in alteredColl)
		{
			// Look for the entity in the context - this action will allow us to determine it's state
			EntityEntry<TTargetEntity> dbEntity = Context.Value.Entry(entity);

			// Determine entities that have been added or modified - in these cases we need to call Save so that any custom
			// repository logic is executed
			if (dbEntity.State is EntityState.Detached or EntityState.Added or EntityState.Modified or EntityState.Unchanged)
			{
				// Do not add children to the context at this point. This still allows us to perform our save
				// logic on the entity, but it also means that should something go wrong that means
				// persisting the parent entity is not valid, we don't end up in a situation where we have
				// child objects as part of the context that shouldn't be saved.
				_ = await repository.SaveAsync(entity, false, false, targetOptions, repoOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
			}
		}
	}
	#endregion

	#region Sanitize Methods		
	/// <summary>
	/// Determines whether the specified entity is empty.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true" /> if it is empty, otherwise <see langword="false" /></returns>
	protected virtual Task<bool> IsEmptyEntityAsync(TEntity entity, TRepoOptions? options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(false);
	}

	/// <summary>
	/// Sanitizes the entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task to await sanitization of the entity.</returns>
	protected virtual Task SanitizeEntityAsync(TEntity entity, TRepoOptions options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(entity);

		entity.TrimAllStringProperties();

		return Task.CompletedTask;
	}
	#endregion

	#endregion

	private void UpdateOriginalConcurrencyStamp(TEntity entity)
	{
		// Setting the ConcurrencyStamp value of the original entry to ensure that the call to save changes will fail
		// if the database value has already changed.
		if (entity is IConcurrencyStamp concurrencyStampEntity)
			Context.Value.Entry(concurrencyStampEntity).Property(x => x.ConcurrencyStamp).OriginalValue = concurrencyStampEntity.ConcurrencyStamp;
	}

	#region Public Methods
	/// <inheritdoc />
	public virtual async Task RemoveEmptyEntitiesAsync(ICollection<TEntity> entities, CancellationToken cancellationToken, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(entities);

		var lstToRemove = new List<TEntity>();

		foreach (TEntity entity in entities)
		{
			if (await IsEmptyEntityAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false))
				lstToRemove.Add(entity);
		}

		foreach (TEntity entity in lstToRemove)
		{
			_ = entities.Remove(entity);

			// Make sure it is removed from the Context if it has just been added - make it detached
			EntityEntry<TEntity> dbEntityEntry = Context.Value.Entry(entity);

			if (dbEntityEntry.State is EntityState.Added)
				dbEntityEntry.State = EntityState.Detached;
		}
	}
	#endregion
}