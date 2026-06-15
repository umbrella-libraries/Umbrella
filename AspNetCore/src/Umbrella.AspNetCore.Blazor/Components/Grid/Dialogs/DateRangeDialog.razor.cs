using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Grid.Dialogs.Models;
using Umbrella.AspNetCore.Blazor.Infrastructure;

namespace Umbrella.AspNetCore.Blazor.Components.Grid.Dialogs;

/// <summary>
/// A dialog used to select a date range.
/// </summary>
/// <seealso cref="UmbrellaDialogComponentBase" />
public partial class DateRangeDialog : UmbrellaDialogComponentBase
{
	/// <summary>
	/// Gets the model used by the edit form on the dialog.
	/// </summary>
	protected DateRangeDialogModel Model { get; } = new();

	/// <summary>
	/// The event handler invoked when the form is submitted.
	/// </summary>
	/// <returns>An awaitable <see cref="Task"/>.</returns>
	protected async Task SubmitFormAsync()
	{
		try
		{
			await ModalInstance.CloseAsync(ModalResult.Ok(Model));
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { Model }))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}
}