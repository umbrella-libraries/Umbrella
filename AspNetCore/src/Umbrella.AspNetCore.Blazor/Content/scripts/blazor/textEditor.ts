import Quill, { type DebugLevel, type Delta, type QuillOptions } from "quill";

type DotNetObjectReference = {
	invokeMethodAsync: (methodName: string, ...args: unknown[]) => Promise<void>;
};

type TextEditorState = {
	quill: Quill;
	blurHandler: () => void;
};

/**
 * Provides Quill-based rich text editor interop for the UmbrellaTextEditor component.
 */
export class TextEditorInterop
{
	#stateMap = new WeakMap<HTMLElement, TextEditorState>();

	/**
	 * Creates a Quill editor instance for the specified element.
	 */
	public create(
		editorElement: HTMLElement,
		toolbarElement: HTMLElement,
		readOnly: boolean,
		placeholder: string,
		theme: string,
		formats: string[] | null,
		debugLevel: string,
		syntax: boolean,
		dotNetObjectReference: DotNetObjectReference): void
	{
		this.dispose(editorElement);

		const options: QuillOptions = {
			debug: this.getDebugLevel(debugLevel),
			modules: {
				syntax,
				toolbar: toolbarElement
			},
			placeholder,
			readOnly,
			theme
		};

		if (formats && formats.length > 0)
			options.formats = formats;

		const quill = new Quill(editorElement, options);

		const notifyDeltaChangedAsync = async () =>
		{
			if (quill.options.debug === "info")
				console.log(`info: Quill editor blur event for ${editorElement.id}`);

			await dotNetObjectReference.invokeMethodAsync("DeltaChanged", this.getContent(editorElement));
		};

		// Wrapped so the listener is void-returning: an async handler passed
		// directly to addEventListener leaves its rejection unhandled.
		const blurHandler = () => void notifyDeltaChangedAsync();

		quill.root.addEventListener("blur", blurHandler);
		this.#stateMap.set(editorElement, { quill, blurHandler });
	}

	/**
	 * Gets the current editor content as Quill Delta JSON.
	 */
	public getContent(editorElement: HTMLElement): string
	{
		return JSON.stringify(this.getState(editorElement).quill.getContents());
	}

	/**
	 * Gets the current editor content as plain text.
	 */
	public getText(editorElement: HTMLElement): string
	{
		return this.getState(editorElement).quill.getText();
	}

	/**
	 * Gets the current editor content as HTML.
	 */
	public getHTML(editorElement: HTMLElement): string
	{
		return this.getState(editorElement).quill.root.innerHTML;
	}

	/**
	 * Loads Quill Delta JSON into the editor.
	 */
	public loadContent(editorElement: HTMLElement, content: string): void
	{
		this.getState(editorElement).quill.setContents(JSON.parse(content) as Delta, "api");
	}

	/**
	 * Loads HTML content into the editor.
	 */
	public loadHTMLContent(editorElement: HTMLElement, htmlContent: string): void
	{
		this.getState(editorElement).quill.root.innerHTML = htmlContent;
	}

	/**
	 * Inserts an image at the current editor selection.
	 */
	public insertImage(editorElement: HTMLElement, imageUrl: string): void
	{
		const quill = this.getState(editorElement).quill;
		const selection = quill.getSelection();
		const editorIndex = selection?.index ?? 0;

		quill.insertEmbed(editorIndex, "image", imageUrl, "api");
	}

	/**
	 * Inserts text at the current editor selection, replacing any selected text.
	 */
	public insertText(editorElement: HTMLElement, text: string): void
	{
		const quill = this.getState(editorElement).quill;
		const selection = quill.getSelection();
		const editorIndex = selection?.index ?? 0;
		const selectionLength = selection?.length ?? 0;

		if (selectionLength > 0)
			quill.deleteText(editorIndex, selectionLength, "api");

		quill.insertText(editorIndex, text, "api");
	}

	/**
	 * Enables or disables the editor.
	 */
	public enable(editorElement: HTMLElement, mode: boolean): void
	{
		this.getState(editorElement).quill.enable(mode);
	}

	/**
	 * Removes event listeners and unregisters editor state.
	 */
	public dispose(editorElement: HTMLElement): void
	{
		const state = this.#stateMap.get(editorElement);

		if (!state)
			return;

		state.quill.root.removeEventListener("blur", state.blurHandler);
		this.#stateMap.delete(editorElement);
	}

	private getState(editorElement: HTMLElement): TextEditorState
	{
		const state = this.#stateMap.get(editorElement);

		if (!state)
			throw new Error("The specified Umbrella text editor has not been initialized.");

		return state;
	}

	private getDebugLevel(debugLevel: string): DebugLevel
	{
		switch (debugLevel)
		{
			case "error":
			case "warn":
			case "log":
			case "info":
				return debugLevel;
			default:
				return "info";
		}
	}
}
