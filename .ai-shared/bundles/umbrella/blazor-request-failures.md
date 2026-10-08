# Blazor request failures and component cancellation

Read this when a Blazor component owns asynchronous loading, saving, uploads, or other requests. Apply it at the UI boundary; a service's exception policy still follows its own contract.

## Distinguish navigation cancellation from a timeout

`HttpClient` timeouts can throw `TaskCanceledException` / `OperationCanceledException` even though the token supplied by the component is not cancelled. `Logger.WriteError` defaults to `ignoreCancellationExceptions: true`, so using its default exception filter can bypass the UI's error handler and leave a loading state active. Its return value also depends on logging configuration; it should not decide whether cancellation was expected.

Check the component's own lifetime token before logging. Suppress cancellation only when the exception is an `OperationCanceledException` (including `TaskCanceledException`) and that token is cancelled. Otherwise allow the UI error handler to run, including for a timeout with an uncancelled token:

```csharp
catch (Exception exc) when (
    !(exc is OperationCanceledException && CancellationToken.IsCancellationRequested)
    && Logger.WriteError(exc, new { Id }, ignoreCancellationExceptions: false))
{
    CurrentState = LayoutState.Error;
    // Set the inline load error message, or handle an action failure using the app's UI.
}
```

Keep the native `Logger.WriteError` call and meaningful state in the filter for analyzer compatibility. Do not replace it with a logging wrapper that the installed analyzer does not recognise. Expected navigation cancellation should propagate without a timeout message or dialog.

Pass the component's `CancellationToken` to component-owned service calls and asynchronous mappings that accept one. Use named `cancellationToken: CancellationToken` arguments where other optional parameters make the position ambiguous. Apply the same rule to lookup, create, update, delete, upload, and enrichment calls. If an operation intentionally outlives the component, use its documented ownership contract instead.

## Complete the UI state transition

For a load owned by the page, set `LayoutState.Loading` and reset the prior error message at the start of each attempt, including retry. Every handled load failure must set `LayoutState.Error`; showing a dialog alone is not a terminal loading state. Set the error state before awaiting any optional error dialog, so a failed or delayed dialog does not keep the page loading.

Prefer an inline error view with a retry callback for page-load failures. With `UmbrellaModelLayoutStateView`, supply an `Error` fragment containing `ErrorStateView Message="@LoadErrorMessage" OnReloadButtonClick="ReloadAsync"`. Its default error fragment already provides a generic reload action, but a custom fragment is needed to display a timeout-specific message. Use the app's existing message constants or resources when available. Save/upload failures should follow the existing action UI and leave the form usable; do not automatically turn every action failure into a page-load error.

An uncancelled `OperationCanceledException`, a `TimeoutException`, or an HTTP operation result with status 408/504 can use a message such as "The server took too long to respond. Please try again." Other failures should use the appropriate operation-result message or a useful generic error. Match the actual operation-result contract; do not assume every service returns HTTP problem details.

If a page redirects for a missing resource or an expired search session, require explicit evidence from its API contract, such as `OperationResultStatus.NotFound` or HTTP 404. A timeout, HTTP 500/503, or another failed result does not establish either condition. Retain the error view and retry behaviour for transient failures.

## Use the framework lifetime

`UmbrellaComponentBase` links its lifetime token to the request-aborted token, cancels active work on disposal, and preserves the token for handlers that finish after disposal. The corrected disposal behaviour is available from `10.0.0-preview-0109`. Check the consuming package version when investigating navigation cancellation; upgrade to the framework fix rather than adding an application-level cancellation-source replacement.

Use the inherited lifetime token in client, dialog, and grid subclasses. When overriding disposal for additional resources, retain the base disposal call. Do not dispose the inherited token's source yourself or create a fresh active lifetime after the component has been disposed.

## Verify the behaviour being changed

When adding or changing a request handler, exercise the relevant cases with the existing component-test or browser harness:

- A request times out while the component token is uncancelled: loading ends, an error is visible, and retry can succeed.
- The page is disposed while a request is pending: the request is cancelled, late handlers can read the cancelled component token, and no timeout error is presented for navigation.
- A transient server failure remains on the page with an error; a genuine missing-resource/session result retains the page's intended redirect or missing-state behaviour.

For an HTTP timeout regression, use a pending `HttpMessageHandler` with a real `HttpClient` timeout, not only a manually thrown cancellation exception. For navigation, dispose the actual component rather than testing only cancellation of its parent token. Keep the handler local; these checks do not require a live backend.
