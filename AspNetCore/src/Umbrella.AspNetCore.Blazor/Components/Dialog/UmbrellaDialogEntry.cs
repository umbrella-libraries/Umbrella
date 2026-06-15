namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// Represents a dialog currently being shown by the dialog system.
/// </summary>
public sealed class UmbrellaDialogEntry
{
	/// <summary>
	/// Gets the type of the Blazor component rendered inside the dialog.
	/// </summary>
	public Type ComponentType { get; }

	/// <summary>
	/// Gets the parameters passed to the dialog component.
	/// </summary>
	public ModalParameters Parameters { get; }

	/// <summary>
	/// Gets the instance used to control and observe this dialog.
	/// </summary>
	public UmbrellaDialogInstance Instance { get; }

	internal UmbrellaDialogEntry(Type componentType, ModalParameters parameters, UmbrellaDialogInstance instance)
	{
		ComponentType = componentType;
		Parameters = parameters;
		Instance = instance;
	}
}
