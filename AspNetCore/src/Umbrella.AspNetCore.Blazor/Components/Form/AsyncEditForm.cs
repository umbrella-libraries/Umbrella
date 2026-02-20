using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components.Forms;
using Umbrella.AspNetCore.Blazor.Components.Validation;

namespace Umbrella.AspNetCore.Blazor.Components.Form;

/// <summary>
/// Provides an EditForm component that supports asynchronous form submission handling in Blazor applications.
/// </summary>
public class AsyncEditForm : EditForm, IDisposable
{
	private bool _disposedValue;

	/// <summary>
	/// Initializes a new instance of the AsyncEditForm class, enabling asynchronous form submission handling.
	/// </summary>
	/// <remarks>
	/// This constructor replaces the default synchronous submission handler of the base EditForm with an asynchronous
	/// handler. This allows the form to support asynchronous operations during submission, such as awaiting long-running
	/// tasks or external service calls. Use this class when you need to perform asynchronous logic as part of the form
	/// submission process.
	/// </remarks>
	public AsyncEditForm()
	{
		// Replace the private readonly delegate in the base EditForm with our async handler.
		// Field: private readonly Func<Task> _handleSubmitDelegate;
		// This enables the form to use our async submission logic.
		FieldInfo? field = typeof(EditForm).GetField("_handleSubmitDelegate", BindingFlags.Instance | BindingFlags.NonPublic);

		// Assign our handler. For readonly fields, SetValue works for reference types.
		field?.SetValue(this, new Func<Task>(HandleSubmitAsync));
	}

	/// <inheritdoc />
	protected override void OnAfterRender(bool firstRender)
	{
		base.OnAfterRender(firstRender);

		if (firstRender)
		{
			if (EditContext?.Properties.TryGetValue(nameof(ObjectGraphDataAnnotationsValidator), out object? value) is true && value is ObjectGraphDataAnnotationsValidator validator)
			{
				validator.OnObjectValidationCompleted += Validator_OnObjectValidationCompleted;
			}
		}
	}

	[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Event handlers.")]
	private async void Validator_OnObjectValidationCompleted(object? sender, EventArgs e)
	{
		if (EditContext is null)
			throw new InvalidOperationException("EditContext cannot be null.");

		bool isValid = !EditContext.GetValidationMessages().Any();

		if (isValid && OnValidSubmit.HasDelegate)
		{
			await OnValidSubmit.InvokeAsync(EditContext);
		}

		if (!isValid && OnInvalidSubmit.HasDelegate)
		{
			await OnInvalidSubmit.InvokeAsync(EditContext);
		}
	}

	private async Task HandleSubmitAsync()
	{
		Debug.Assert(EditContext != null);

		if (OnSubmit.HasDelegate)
		{
			// When using OnSubmit, the developer takes control of the validation lifecycle
			await OnSubmit.InvokeAsync(EditContext);
		}
		else
		{
			// Otherwise, the system implicitly runs validation on form submission
			_ = EditContext.Validate(); // This will likely become ValidateAsync later
		}
	}

	/// <summary>
	/// Releases the unmanaged resources used by the object and optionally releases the managed resources.
	/// </summary>
	/// <remarks>
	/// This method is called by public Dispose methods and finalizers to perform resource cleanup. When disposing is true,
	/// this method can dispose managed resources in addition to unmanaged resources. Override this method to release
	/// additional resources in derived classes.
	/// </remarks>
	/// <param name="disposing">
	/// true to release both managed and unmanaged resources; false to release only unmanaged resources.
	/// </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				if (EditContext?.Properties.TryGetValue(nameof(ObjectGraphDataAnnotationsValidator), out object? value) is true && value is ObjectGraphDataAnnotationsValidator validator)
				{
					validator.OnObjectValidationCompleted -= Validator_OnObjectValidationCompleted;
				}
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