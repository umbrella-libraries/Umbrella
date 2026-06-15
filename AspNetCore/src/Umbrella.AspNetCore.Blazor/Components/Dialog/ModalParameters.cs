using System.Collections;

namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// A named parameter bag passed to a dialog component.
/// </summary>
public sealed class ModalParameters : IEnumerable<KeyValuePair<string, object?>>
{
	private readonly Dictionary<string, object?> _items = [];

	/// <summary>
	/// Adds or replaces a parameter.
	/// </summary>
	public void Add(string name, object? value) => _items[name] = value;

	/// <summary>
	/// Gets a parameter value by name, casting to <typeparamref name="T"/>.
	/// </summary>
	public T Get<T>(string name) => (T)_items[name]!;

	/// <summary>
	/// Tries to get a parameter value by name.
	/// </summary>
	public bool TryGet<T>(string name, out T? value)
	{
		if (_items.TryGetValue(name, out object? obj))
		{
			value = (T?)obj;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// Returns the parameters as a dictionary suitable for <see cref="DynamicComponent.Parameters"/>.
	/// </summary>
	public IDictionary<string, object?> ToDictionary() => new Dictionary<string, object?>(_items);

	/// <inheritdoc/>
	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _items.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}