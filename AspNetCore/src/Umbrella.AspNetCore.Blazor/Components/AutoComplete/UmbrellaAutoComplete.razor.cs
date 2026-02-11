using CommunityToolkit.Diagnostics;

namespace Umbrella.AspNetCore.Blazor.Components.AutoComplete;

/// <summary>
/// A component used to provide autocomplete functionality to a text input field.
/// </summary>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IDisposable" />
public partial class UmbrellaAutoComplete : IDisposable
{
	private readonly string _id = Guid.NewGuid().ToString("N").ToLowerInvariant();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private bool _disposedValue;
	private bool _valueHasChanged;
	private readonly HashSet<string> _options = [];

	/// <summary>
	/// Gets or sets the label text.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public string? LabelText { get; set; }

	/// <summary>
	/// Gets or sets the value.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public string? Value { get; set; }

	/// <summary>
	/// Gets or sets the search method.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public Func<string?, Task<IEnumerable<string>>>? SearchMethod { get; set; }

	/// <summary>
	/// Gets or sets the debounce in milliseconds.
	/// </summary>
	/// <remarks>Defaults to <c>300ms</c></remarks>
	[Parameter]
	public int Debounce { get; set; } = 300;

	/// <summary>
	/// Gets or sets the maximum suggestions that will be displayed to the user.
	/// </summary>
	/// <remarks>Defaults to <c>10</c></remarks>
	[Parameter]
	public int MaximumSuggestions { get; set; } = 10;

	/// <summary>
	/// Gets or sets the minimum characters that need to be provided before the <see cref="SearchMethod"/> delegate is invoked.
	/// </summary>
	/// <remarks>Defaults to <c>3</c></remarks>
	[Parameter]
	public int MinimumLength { get; set; } = 3;

	/// <summary>
	/// Gets or sets the value changed delegate that is invoked when the <see cref="Value"/> changes.
	/// </summary>
	[Parameter]
	public EventCallback<string?> ValueChanged { get; set; }

	/// <inheritdoc/>
	protected override void OnParametersSet()
	{
		base.OnParametersSet();
		Guard.IsNotNull(SearchMethod);
		Guard.IsLessThan(0, Debounce);
		Guard.IsLessThan(1, MinimumLength);
		Guard.IsLessThan(1, MaximumSuggestions);
	}

	/// <inheritdoc/>
	protected override async Task OnInitializedAsync()
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Debounce));

		do
		{
			if (_valueHasChanged)
			{
				if (Value is not null && Value.Length >= MinimumLength)
				{
					IEnumerable<string> lstResult = await SearchMethod!.Invoke(Value);

					_options.Clear();
					_options.AddRange(lstResult.OrderBy(x => x).Take(MaximumSuggestions));

					StateHasChanged();
				}
			}

			_valueHasChanged = false;
		}
		while (await timer.WaitForNextTickAsync(_cancellationTokenSource.Token));
	}

	private void OnInput(ChangeEventArgs args)
	{
		_valueHasChanged = true;
		_options.Clear();

		Value = args.Value?.ToString();
	}

	private async Task OnChangeAsync(ChangeEventArgs args)
	{
		_valueHasChanged = false;

		await ValueChanged.InvokeAsync(Value);
	}

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_cancellationTokenSource.Cancel();
				_cancellationTokenSource.Dispose();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}