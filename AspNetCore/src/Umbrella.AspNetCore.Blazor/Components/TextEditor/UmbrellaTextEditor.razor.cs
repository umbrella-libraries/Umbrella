using System.ComponentModel;

namespace Umbrella.AspNetCore.Blazor.Components.TextEditor;

/// <summary>
/// A Quill-based rich text editor component for Blazor applications.
/// </summary>
/// <remarks>
/// Add the generated Umbrella text editor assets to the consuming app:
/// <c>_content/Umbrella.AspNetCore.Blazor/dist/umbrella-blazor-text-editor.css</c> and
/// <c>_content/Umbrella.AspNetCore.Blazor/dist/umbrella-blazor-text-editor.js</c>.
/// This component uses npm package <c>quill@2.0.3</c>. The optional Quill syntax module is passed through
/// unchanged; consuming apps must provide any additional syntax-highlighting support required by Quill.
/// Adapted from the MIT-licensed Blazored.TextEditor project.
/// </remarks>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IAsyncDisposable" />
public partial class UmbrellaTextEditor : IAsyncDisposable
{
	private const string InteropObjectPath = "UmbrellaBlazorTextEditorInterop";

	private readonly string _generatedEditorId = $"umbrella-text-editor-{Guid.NewGuid():N}".ToLowerInvariant();
	private ElementReference _editorElement;
	private ElementReference _toolbarElement;
	private DotNetObjectReference<UmbrellaTextEditor>? _objectReference;
	private bool _isInitialized;
	private bool _pendingContentLoad;
	private string? _lastLoadedContent;

	[Inject]
	private IJSRuntime JSRuntime { get; set; } = null!;

	/// <summary>
	/// Gets or sets a custom id for the editor element. A collision-safe id is generated when this is not specified.
	/// </summary>
	[Parameter]
	public string? EditorId { get; set; }

	/// <summary>
	/// Gets or sets a custom id for the toolbar element. A collision-safe id based on <see cref="EditorId"/> is generated when this is not specified.
	/// </summary>
	[Parameter]
	public string? ToolbarId { get; set; }

	/// <summary>
	/// Gets or sets initial editor markup rendered before Quill initializes.
	/// </summary>
	[Parameter]
	public RenderFragment? EditorContent { get; set; }

	/// <summary>
	/// Gets or sets the Quill toolbar markup.
	/// </summary>
	[Parameter]
	public RenderFragment? ToolbarContent { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the editor is read-only.
	/// </summary>
	[Parameter]
	public bool ReadOnly { get; set; }

	/// <summary>
	/// Gets or sets the placeholder text shown when the editor is empty.
	/// </summary>
	[Parameter]
	public string Placeholder { get; set; } = "Compose an epic...";

	/// <summary>
	/// Gets or sets the Quill theme. Common values are <c>snow</c> and <c>bubble</c>.
	/// </summary>
	[Parameter]
	public string Theme { get; set; } = "snow";

	/// <summary>
	/// Gets or sets the Quill formats allowed by the editor.
	/// </summary>
	[Parameter]
	public IReadOnlyList<string>? Formats { get; set; }

	/// <summary>
	/// Gets or sets the Quill debug level. Common values are <c>error</c>, <c>warn</c>, <c>log</c>, and <c>info</c>.
	/// </summary>
	[Parameter]
	public string DebugLevel { get; set; } = "info";

	/// <summary>
	/// Gets or sets additional CSS classes for the editor element.
	/// </summary>
	[Parameter]
	public string EditorCssClass { get; set; } = "";

	/// <summary>
	/// Gets or sets inline CSS styles for the editor element.
	/// </summary>
	[Parameter]
	public string EditorCssStyle { get; set; } = "";

	/// <summary>
	/// Gets or sets additional CSS classes for the toolbar element.
	/// </summary>
	[Parameter]
	public string ToolbarCssClass { get; set; } = "";

	/// <summary>
	/// Gets or sets inline CSS styles for the toolbar element.
	/// </summary>
	[Parameter]
	public string ToolbarCssStyle { get; set; } = "";

	/// <summary>
	/// Gets or sets a value indicating whether the toolbar should be rendered below the editor.
	/// </summary>
	[Parameter]
	public bool BottomToolbar { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the Quill syntax module should be enabled.
	/// </summary>
	[Parameter]
	public bool Syntax { get; set; }

	/// <summary>
	/// Gets or sets the editor content as Quill Delta JSON.
	/// </summary>
	[Parameter]
	public string? Content { get; set; }

	/// <summary>
	/// Gets or sets the callback invoked with Quill Delta JSON when the editor loses focus.
	/// </summary>
	[Parameter]
	public EventCallback<string> ContentChanged { get; set; }

	private string ResolvedEditorId => string.IsNullOrWhiteSpace(EditorId) ? _generatedEditorId : EditorId;

	private string ResolvedToolbarId => string.IsNullOrWhiteSpace(ToolbarId) ? $"{ResolvedEditorId}-toolbar" : ToolbarId;

	/// <inheritdoc />
	protected override void OnInitialized()
	{
		_objectReference = DotNetObjectReference.Create(this);
	}

	/// <inheritdoc />
	protected override void OnParametersSet()
	{
		if (Content != _lastLoadedContent)
			_pendingContentLoad = true;
	}

	/// <inheritdoc />
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
			await JSRuntime.InvokeVoidAsync(
				$"{InteropObjectPath}.create",
				_editorElement,
				_toolbarElement,
				ReadOnly,
				Placeholder,
				Theme,
				Formats,
				DebugLevel,
				Syntax,
				_objectReference);

			_isInitialized = true;
		}

		if (_isInitialized && _pendingContentLoad)
			await LoadContentFromParameterAsync();
	}

	/// <summary>
	/// Gets the editor content as plain text.
	/// </summary>
	/// <returns>The editor content as plain text.</returns>
	public async Task<string> GetTextAsync() => await GetTextAsync(default);

	/// <summary>
	/// Gets the editor content as plain text.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The editor content as plain text.</returns>
	public async Task<string> GetTextAsync(CancellationToken cancellationToken)
		=> await JSRuntime.InvokeAsync<string>($"{InteropObjectPath}.getText", cancellationToken, _editorElement);

	/// <summary>
	/// Gets the editor content as HTML.
	/// </summary>
	/// <returns>The editor content as HTML.</returns>
	public async Task<string> GetHTMLAsync() => await GetHTMLAsync(default);

	/// <summary>
	/// Gets the editor content as HTML.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The editor content as HTML.</returns>
	public async Task<string> GetHTMLAsync(CancellationToken cancellationToken)
		=> await JSRuntime.InvokeAsync<string>($"{InteropObjectPath}.getHTML", cancellationToken, _editorElement);

	/// <summary>
	/// Gets the editor content as Quill Delta JSON.
	/// </summary>
	/// <returns>The editor content as Quill Delta JSON.</returns>
	public async Task<string> GetContentAsync() => await GetContentAsync(default);

	/// <summary>
	/// Gets the editor content as Quill Delta JSON.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The editor content as Quill Delta JSON.</returns>
	public async Task<string> GetContentAsync(CancellationToken cancellationToken)
		=> await JSRuntime.InvokeAsync<string>($"{InteropObjectPath}.getContent", cancellationToken, _editorElement);

	/// <summary>
	/// Loads Quill Delta JSON into the editor.
	/// </summary>
	/// <param name="content">The Quill Delta JSON content.</param>
	public async Task LoadContentAsync(string content) => await LoadContentAsync(content, default);

	/// <summary>
	/// Loads Quill Delta JSON into the editor.
	/// </summary>
	/// <param name="content">The Quill Delta JSON content.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task LoadContentAsync(string content, CancellationToken cancellationToken)
	{
		await JSRuntime.InvokeVoidAsync($"{InteropObjectPath}.loadContent", cancellationToken, _editorElement, content);
		_lastLoadedContent = content;
	}

	/// <summary>
	/// Loads HTML content into the editor.
	/// </summary>
	/// <param name="htmlContent">The HTML content.</param>
	public async Task LoadHTMLContentAsync(string htmlContent) => await LoadHTMLContentAsync(htmlContent, default);

	/// <summary>
	/// Loads HTML content into the editor.
	/// </summary>
	/// <param name="htmlContent">The HTML content.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task LoadHTMLContentAsync(string htmlContent, CancellationToken cancellationToken)
		=> await JSRuntime.InvokeVoidAsync($"{InteropObjectPath}.loadHTMLContent", cancellationToken, _editorElement, htmlContent);

	/// <summary>
	/// Inserts an image at the current editor selection.
	/// </summary>
	/// <param name="imageUrl">The image URL.</param>
	public async Task InsertImageAsync(string imageUrl) => await InsertImageAsync(imageUrl, default);

	/// <summary>
	/// Inserts an image at the current editor selection.
	/// </summary>
	/// <param name="imageUrl">The image URL.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task InsertImageAsync(string imageUrl, CancellationToken cancellationToken)
		=> await JSRuntime.InvokeVoidAsync($"{InteropObjectPath}.insertImage", cancellationToken, _editorElement, imageUrl);

	/// <summary>
	/// Inserts text at the current editor selection.
	/// </summary>
	/// <param name="text">The text to insert.</param>
	public async Task InsertTextAsync(string text) => await InsertTextAsync(text, default);

	/// <summary>
	/// Inserts text at the current editor selection.
	/// </summary>
	/// <param name="text">The text to insert.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task InsertTextAsync(string text, CancellationToken cancellationToken)
		=> await JSRuntime.InvokeVoidAsync($"{InteropObjectPath}.insertText", cancellationToken, _editorElement, text);

	/// <summary>
	/// Enables or disables the editor.
	/// </summary>
	/// <param name="mode"><c>true</c> to enable the editor; otherwise, <c>false</c>.</param>
	public async Task EnableEditorAsync(bool mode) => await EnableEditorAsync(mode, default);

	/// <summary>
	/// Enables or disables the editor.
	/// </summary>
	/// <param name="mode"><c>true</c> to enable the editor; otherwise, <c>false</c>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task EnableEditorAsync(bool mode, CancellationToken cancellationToken)
		=> await JSRuntime.InvokeVoidAsync($"{InteropObjectPath}.enable", cancellationToken, _editorElement, mode);

	/// <summary>
	/// Handles Quill Delta change notifications from JavaScript.
	/// </summary>
	/// <param name="content">The Quill Delta JSON content.</param>
	/// <returns>A task that represents the asynchronous callback operation.</returns>
	[JSInvokable("DeltaChanged")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public async Task OnChangeAsync(string content)
	{
		if (string.Equals(DebugLevel, "info", StringComparison.OrdinalIgnoreCase))
		{
			Console.WriteLine($"Quill editor element: {ResolvedEditorId} invoked change in Blazor.");
			Console.WriteLine("    Contents:" + content);
		}

		Content = content;
		_lastLoadedContent = content;

		await ContentChanged.InvokeAsync(content);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		try
		{
			if (_isInitialized)
				await JSRuntime.InvokeVoidAsync($"{InteropObjectPath}.dispose", _editorElement);
		}
		catch (InvalidOperationException)
		{
			// JS interop can be unavailable during prerendering or after the circuit has ended.
		}
		catch (JSDisconnectedException)
		{
			// Blazor Server circuit already disconnected; browser-side state is gone.
		}
		finally
		{
			_objectReference?.Dispose();
			GC.SuppressFinalize(this);
		}
	}

	private async Task LoadContentFromParameterAsync()
	{
		_pendingContentLoad = false;

		if (!string.IsNullOrWhiteSpace(Content))
			await LoadContentAsync(Content);

		_lastLoadedContent = Content;
	}
}
