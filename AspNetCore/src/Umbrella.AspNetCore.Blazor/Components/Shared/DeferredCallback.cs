namespace Umbrella.AspNetCore.Blazor.Components.Shared;

/// <summary>
/// Invokes a callback once when the component is initialized.
/// </summary>
public sealed class DeferredCallback : ComponentBase
{
    private bool _invoked;

    /// <summary>
    /// Gets or sets the callback to invoke when initialization has completed.
    /// </summary>
    [Parameter]
    public EventCallback OnReady { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (_invoked)
            return;

        _invoked = true;

        if (OnReady.HasDelegate)
            await OnReady.InvokeAsync();
    }
}
