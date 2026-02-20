using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Diagnostics;
using Umbrella.DataAnnotations;
using Umbrella.Utilities.Annotations.Services.Abstractions;
using Umbrella.Utilities.Primitives;

namespace Umbrella.Utilities.Annotations;

/// <summary>
/// Provides an abstract base class for creating asynchronous validation attributes that delegate validation logic to a
/// specified service type.
/// </summary>
/// <remarks>This attribute is intended for scenarios where validation must be performed asynchronously using a
/// service resolved from the validation context. If the required service cannot be resolved, an
/// InvalidOperationException is thrown. Derived attributes should specify the service type via the generic
/// parameter.</remarks>
/// <typeparam name="T">The type of the validation service that implements the <see cref="IUmbrellaValidationServiceAttributeService"/> interface and
/// performs the validation logic.</typeparam>
public abstract class UmbrellaValidationServiceAttribute<T> : AsyncValidationAttribute
	where T : IUmbrellaValidationServiceAttributeService
{
	/// <inheritdoc />
	protected override async Task<ValidationResult?> IsValidAsync(object? value, ValidationContext validationContext, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(validationContext);

		T service = validationContext.GetService(typeof(T)) is not T resolvedService
			? throw new InvalidOperationException($"Service of type {typeof(T).FullName} could not be resolved.")
			: resolvedService;

		var result = await service.IsValidAsync(validationContext.ObjectInstance, value, ErrorMessage).ConfigureAwait(false);

		if (result.Status is OperationResultStatus.GenericSuccess)
			return ValidationResult.Success;

		return new ValidationResult(result.PrimaryValidationMessage, new[] { validationContext.MemberName ?? "" });
	}
}