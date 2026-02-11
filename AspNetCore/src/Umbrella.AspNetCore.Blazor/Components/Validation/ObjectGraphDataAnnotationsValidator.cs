using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.DataAnnotations.Helpers;
using Umbrella.Utilities.DataAnnotations.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.Validation;

/// <summary>
/// A component that performs validation of an entire object graph internally using the <see cref="IObjectGraphValidator"/>.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IDisposable" />
public class ObjectGraphDataAnnotationsValidator : ComponentBase, IDisposable
{
	private ValidationMessageStore? _validationMessageStore;
	private bool _disposedValue;

	[Inject]
	private IObjectGraphValidator ObjectGraphValidator { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private IServiceProvider ServiceProvider { get; set; } = null!;

	[CascadingParameter]
	private EditContext? EditContext { get; set; }

	/// <summary>
	/// Occurs when the validation process for an object has completed.
	/// </summary>
	/// <remarks>Subscribers can use this event to perform actions or respond after object validation is finished.
	/// The event is raised regardless of whether validation succeeded or failed.
	/// </remarks>
	public event EventHandler<EventArgs>? OnObjectValidationCompleted;

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		if (EditContext is null)
			return;

		_validationMessageStore = new ValidationMessageStore(EditContext);

		// Perform object-level validation (starting from the root model) on request
		EditContext.OnValidationRequested += HandleOnValidationRequested;

		// Perform per-field validation on each field edit
		EditContext.OnFieldChanged += HandleOnFieldChanged;

		// Register this validator instance in the EditContext for potential use by other components
		EditContext.Properties[nameof(ObjectGraphDataAnnotationsValidator)] = this;
	}

	private async Task ValidateObjectAsync(object value)
	{
		if (value is null || _validationMessageStore is null || EditContext is null)
			return;

		// Construct a root ValidationContext similar to UmbrellaValidator usage.
		var validationContext = new ValidationContext(value, ServiceProvider, null);

		var (_, results) = await ObjectGraphValidator.TryValidateObjectAsync(value, validationContext, validateAllProperties: true, serviceProvider: ServiceProvider);

		// Transfer results to the ValidationMessageStore
		foreach (var validationResult in results)
		{
			string errorMessage = validationResult.ErrorMessage ?? "Validation Error.";

			if (!validationResult.MemberNames.Any())
			{
				_validationMessageStore.Add(new FieldIdentifier(value, string.Empty), errorMessage);

				continue;
			}

			foreach (string memberName in validationResult.MemberNames)
			{
				var fieldIdentifier = new FieldIdentifier(validationResult.Model, memberName);
				_validationMessageStore.Add(fieldIdentifier, errorMessage);
			}
		}

		// We have to notify even if there were no messages before and are still no messages now,
		// because the "state" that changed might be the completion of some async validation task
		EditContext.NotifyValidationStateChanged();

		OnObjectValidationCompleted?.Invoke(this, EventArgs.Empty);
	}

	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Fine for event handlers.")]
	private async void ValidateField(FieldIdentifier fieldIdentifier)
	{
		if (EditContext is null || _validationMessageStore is null)
			return;

		// DataAnnotations only validates public properties, so that's all we'll look for
		var propertyInfo = fieldIdentifier.Model.GetType().GetProperty(fieldIdentifier.FieldName);

		if (propertyInfo is not null)
		{
			object? propertyValue = propertyInfo.GetValue(fieldIdentifier.Model);

			var validationContext = new ValidationContext(fieldIdentifier.Model, ServiceProvider, null)
			{
				MemberName = propertyInfo.Name
			};

			var results = new List<ValidationResult>();

			_ = await AsyncValidator.TryValidatePropertyAsync(propertyValue, validationContext, results);

			_validationMessageStore.Clear(fieldIdentifier);
			_validationMessageStore.Add(fieldIdentifier, results.Select(result => result.ErrorMessage ?? "Error Message."));

			// We have to notify even if there were no messages before and are still no messages now,
			// because the "state" that changed might be the completion of some async validation task
			EditContext.NotifyValidationStateChanged();
		}
	}

	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Fine for event handlers.")]
	private async void HandleOnValidationRequested(object? sender, ValidationRequestedEventArgs eventArgs)
	{
		_validationMessageStore?.Clear();

		if (EditContext is not null)
		{
			await ValidateObjectAsync(EditContext.Model);
		}
	}

	private void HandleOnFieldChanged(object? sender, FieldChangedEventArgs eventArgs) => ValidateField(eventArgs.FieldIdentifier);

	/// <summary>
	/// Disposes of this instance.
	/// </summary>
	/// <param name="disposing">Specifies if this instance is being disposed.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				// Detach event handlers and clean up resources
				if (EditContext is not null)
				{
					EditContext.OnValidationRequested -= HandleOnValidationRequested;
					EditContext.OnFieldChanged -= HandleOnFieldChanged;
				}

				_validationMessageStore = null;
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}