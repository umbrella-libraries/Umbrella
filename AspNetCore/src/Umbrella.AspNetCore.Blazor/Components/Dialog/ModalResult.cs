namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// Represents the result of a dialog operation.
/// </summary>
public class ModalResult
{
	/// <summary>
	/// Gets the data returned by the dialog when it was closed successfully.
	/// </summary>
	public object? Data { get; }

	/// <summary>
	/// Gets a value indicating whether the dialog was cancelled.
	/// </summary>
	public bool Cancelled { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ModalResult"/> class.
	/// </summary>
	protected ModalResult(object? data, bool cancelled)
	{
		Data = data;
		Cancelled = cancelled;
	}

	/// <summary>
	/// Creates a successful (non-cancelled) result, optionally carrying data.
	/// </summary>
	public static ModalResult Ok(object? data = null) => new(data, false);

	/// <summary>
	/// Creates a cancelled result.
	/// </summary>
	public static ModalResult Cancel() => new(null, true);
}
