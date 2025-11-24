// Copyright (c) Zinofi Digital Ltd. All Rights Reserved.
// Licensed under the MIT License.

using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Security.Policies;

namespace Umbrella.DataAccess.Abstractions.Options;

/// <summary>
/// Options for use with the <see cref="IUmbrellaRepositoryCoreDataService"/>
/// </summary>
/// <seealso cref="ISanitizableUmbrellaOptions" />
/// <seealso cref="IValidatableUmbrellaOptions" />
public class UmbrellaRepositoryDataServiceOptions : ISanitizableUmbrellaOptions, IValidatableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets the concurrency error message returned concurrency error responses for <see cref="OperationResultStatus.Conflict"/> statuses.
	/// </summary>
	public string ConcurrencyErrorMessage { get; set; } = "This information has been changed elsewhere since this screen was loaded. Please try again.";

	/// <summary>
	/// Gets or sets the exception filter that is invoked when exceptions are thrown inside the <see cref="IUmbrellaRepositoryCoreDataService.ReadAllAsync"/> method.
	/// </summary>
	/// <remarks>
	/// If the exception filter returns <see langword="true"/>, the <see cref="HandleReadAllExceptionAsync"/> delegate is invoked, if it has been specified.
	/// </remarks>
	public Func<Exception, bool> ReadAllExceptionFilter { get; set; } = _ => false;

	/// <summary>
	/// Gets or sets the delegate used to handle exceptions where the <see cref="ReadAllExceptionFilter"/> returns <see langword="true"/>.
	/// If this is set to <see langword="null"/>, the exception is simply re-thrown.
	/// </summary>
	public Func<Exception, Task<OperationResult?>> HandleReadAllExceptionAsync { get; set; } = _ => Task.FromResult<OperationResult?>(null);

	/// <summary>
	/// Gets or sets the exception filter that is invoked when exceptions are thrown inside the <see cref="IUmbrellaRepositoryCoreDataService.ReadAsync"/> method.
	/// </summary>
	/// <remarks>
	/// If the exception filter returns <see langword="true"/>, the <see cref="HandleReadExceptionAsync"/> delegate is invoked, if it has been specified.
	/// </remarks>
	public Func<Exception, bool> ReadExceptionFilter { get; set; } = _ => false;

	/// <summary>
	/// Gets or sets the delegate used to handle exceptions where the <see cref="ReadExceptionFilter"/> returns <see langword="true"/>.
	/// If this is set to <see langword="null"/>, the exception is simply re-thrown.
	/// </summary>
	public Func<Exception, Task<OperationResult?>> HandleReadExceptionAsync { get; set; } = _ => Task.FromResult<OperationResult?>(null);

	/// <summary>
	/// Gets or sets the exception filter that is invoked when exceptions are thrown inside the <see cref="IUmbrellaRepositoryCoreDataService.CreateAsync"/> method.
	/// </summary>
	/// <remarks>
	/// If the exception filter returns <see langword="true"/>, the <see cref="HandleCreateExceptionAsync"/> delegate is invoked, if it has been specified.
	/// </remarks>
	public Func<Exception, bool> CreateExceptionFilter { get; set; } = _ => false;

	/// <summary>
	/// Gets or sets the delegate used to handle exceptions where the <see cref="CreateExceptionFilter"/> returns <see langword="true"/>.
	/// If this is set to <see langword="null"/>, the exception is simply re-thrown.
	/// </summary>
	public Func<Exception, Task<OperationResult?>> HandleCreateExceptionAsync { get; set; } = _ => Task.FromResult<OperationResult?>(null);

	/// <summary>
	/// Gets or sets the exception filter that is invoked when exceptions are thrown inside the <see cref="IUmbrellaRepositoryCoreDataService.UpdateAsync"/> method.
	/// </summary>
	/// <remarks>
	/// If the exception filter returns <see langword="true"/>, the <see cref="HandleUpdateExceptionAsync"/> delegate is invoked, if it has been specified.
	/// </remarks>
	public Func<Exception, bool> UpdateExceptionFilter { get; set; } = _ => false;

	/// <summary>
	/// Gets or sets the delegate used to handle exceptions where the <see cref="UpdateExceptionFilter"/> returns <see langword="true"/>.
	/// If this is set to <see langword="null"/>, the exception is simply re-thrown.
	/// </summary>
	public Func<Exception, Task<OperationResult?>> HandleUpdateExceptionAsync { get; set; } = _ => Task.FromResult<OperationResult?>(null);

	/// <summary>
	/// Gets or sets the exception filter that is invoked when exceptions are thrown inside the <see cref="IUmbrellaRepositoryCoreDataService.DeleteAsync"/> method.
	/// </summary>
	/// <remarks>
	/// If the exception filter returns <see langword="true"/>, the <see cref="HandleDeleteExceptionAsync"/> delegate is invoked, if it has been specified.
	/// </remarks>
	public Func<Exception, bool> DeleteExceptionFilter { get; set; } = _ => false;

	/// <summary>
	/// Gets or sets the delegate used to handle exceptions where the <see cref="DeleteExceptionFilter"/> returns <see langword="true"/>.
	/// If this is set to <see langword="null"/>, the exception is simply re-thrown.
	/// </summary>
	public Func<Exception, Task<OperationResult?>> HandleDeleteExceptionAsync { get; set; } = _ => Task.FromResult<OperationResult?>(null);

	/// <summary>
	/// Gets or sets the name of the authorization policy checked when creating items.
	/// </summary>
	public string CreatePolicyName { get; set; } = CorePolicyNames.Create;

	/// <summary>
	/// Gets or sets the name of the authorization policy checked when reading items.
	/// </summary>
	public string ReadPolicyName { get; set; } = CorePolicyNames.Read;

	/// <summary>
	/// Gets or sets the name of the authorization policy checked when updating items.
	/// </summary>
	public string UpdatePolicyName { get; set; } = CorePolicyNames.Update;

	/// <summary>
	/// Gets or sets the name of the authorization policy checked when deleting items.
	/// </summary>
	public string DeletePolicyName { get; set; } = CorePolicyNames.Delete;

	/// <summary>
	/// Gets or sets a predicate used to determine whether a given object should be excluded from unit of work operations
	/// in data access scenarios.
	/// </summary>
	/// <remarks>If set, the filter is invoked for each object to evaluate whether it should be excluded. Objects
	/// for which the filter returns <see langword="true"/> will not participate in unit of work processing. If the filter
	/// is <see langword="null"/>, no objects are excluded by this mechanism.</remarks>
	public Func<object, bool>? DataAccessUnitOfWorkExclusionFilter { get; set; }

	/// <inheritdoc />
	public void Sanitize()
	{
		ConcurrencyErrorMessage = ConcurrencyErrorMessage?.Trim()!;
		CreatePolicyName = CreatePolicyName?.Trim()!;
		ReadPolicyName = ReadPolicyName?.Trim()!;
		UpdatePolicyName = UpdatePolicyName?.Trim()!;
		DeletePolicyName = DeletePolicyName?.Trim()!;
	}

	/// <inheritdoc />
	public void Validate()
	{
		Guard.IsNotNullOrEmpty(ConcurrencyErrorMessage);
		Guard.IsNotNullOrEmpty(CreatePolicyName);
		Guard.IsNotNullOrEmpty(ReadPolicyName);
		Guard.IsNotNullOrEmpty(UpdatePolicyName);
		Guard.IsNotNullOrEmpty(DeletePolicyName);
	}
}