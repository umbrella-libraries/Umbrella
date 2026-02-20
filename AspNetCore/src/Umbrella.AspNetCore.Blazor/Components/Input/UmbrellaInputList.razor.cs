namespace Umbrella.AspNetCore.Blazor.Components.Input;

/// <summary>
/// A component that renders a list of input items of type <typeparamref name="T"/> . The component allows users to add
/// and remove items from the list. The list is displayed with a heading and an optional description. The component also
/// supports two-way data binding for the list of items through the <see cref="Value"/> property and the
/// <see cref="ValueChanged"/> event callback.
/// </summary>
/// <typeparam name="T">The type of the items in the list.</typeparam>
public partial class UmbrellaInputList<T>
{
	/// <summary>
	/// Gets or sets the list of values of type T for the input component.
	/// </summary>
	/// <remarks>Each element in the list may be null, depending on the type parameter T. The list must be
	/// initialized before use to avoid null reference exceptions. This property is typically bound to the input's value in
	/// a Blazor form.</remarks>
	[Parameter]
	[EditorRequired]
	public List<T?> Value { get; set; } = [];

	/// <summary>
	/// Gets or sets the heading text displayed by the component.
	/// </summary>
	/// <remarks>This property is required and must not be null. The heading typically provides a title or label for
	/// the component's content. Assign a non-null, user-visible string to ensure proper display and
	/// accessibility.</remarks>
	[Parameter]
	[EditorRequired]
	public string Heading { get; set; } = null!;
	
	/// <summary>
	/// Gets or sets the description content displayed by the component.
	/// </summary>
	/// <remarks>This property is optional and can be used to provide additional context or instructions for the component's content.</remarks>
	[Parameter]
	public RenderFragment? Description { get; set; }
	
	/// <summary>
	/// Gets or sets the event callback that is invoked when the value of the component changes.
	/// </summary>
	/// <remarks>This property is typically used for two-way data binding in Blazor forms.</remarks>
	[Parameter]
	public EventCallback<List<T?>> ValueChanged { get; set; }

	// For two-way binding
	private List<T?> Items
	{
		get => Value ??= [];
		set
		{
			Value = value;
			_ = ValueChanged.InvokeAsync(Value);
		}
	}

	private async Task RemoveItemClickAsync(int index)
	{
		Console.WriteLine($"Removing item at index {index}");

		if (index >= 0 && index < Items.Count)
		{
			Items.RemoveAt(index);
			await ValueChanged.InvokeAsync(Items);
			StateHasChanged();
		}
	}

	private async Task AddItemClickAsync()
	{
		Items.Add(default);

		await ValueChanged.InvokeAsync(Items);

		StateHasChanged();
	}
}