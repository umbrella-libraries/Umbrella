/* eslint-disable */
import { BrowserEventAggregator } from './browserEventAggregator';
import { TextEditorInterop } from './textEditor';

/**
 * Provides JavaScript interop helpers used by Blazor components.
 */
export class UmbrellaBlazorInterop
{
	#browserEventAggregator: BrowserEventAggregator | null = null;
	#textEditor: TextEditorInterop | null = null;

	scrollTimeout: number | null = null;
	blazorInteropUtility: any;
	boundScrollTopFunction: any;

	/**
	 * Gets a lazily-initialized browser event aggregator instance.
	 */
	get browserEventAggregator()
	{
		if (this.#browserEventAggregator)
			return this.#browserEventAggregator;

		this.#browserEventAggregator = new BrowserEventAggregator();

		return this.#browserEventAggregator;
	}

	/**
	 * Gets a lazily-initialized text editor interop instance.
	 */
	get textEditor()
	{
		if (this.#textEditor)
			return this.#textEditor;

		this.#textEditor = new TextEditorInterop();

		return this.#textEditor;
	}

	/**
	 * Sets the current document title.
	 * @param title The page title to apply.
	 */
	public setPageTitle(title: string): void
	{
		document.title = title;
	}

	/**
	 * Triggers a click event for the first element matching the selector.
	 * @param selector A valid CSS selector for the element to click.
	 */
	public triggerElementClick(selector: string): void
	{
		(document.querySelector(selector) as HTMLElement)?.click();
	}

	/**
	 * Scrolls the window either to an absolute Y position or to an element's top position.
	 * @param position A Y coordinate or CSS selector used as the scroll target.
	 * @param offset An additional offset to apply to the target position.
	 */
	public scrollTo(position: number | string, offset = 0): void
	{
		if (typeof position === "number")
		{
			let offsetPosition = position + offset;

			if (offsetPosition < 0)
				offsetPosition = 0;

			window.scrollTo(offsetPosition, 0);

			return;
		}

		if (typeof position === "string")
		{
			const target = document.querySelector(position) as HTMLElement;

			if (target)
			{
				let offsetPosition = target.offsetTop + offset;

				if (offsetPosition < 0)
					offsetPosition = 0;

				window.scrollTo(offsetPosition, 0);

				return;
			}
		}
	}

	/**
	 * Scrolls the window near the bottom of the current viewport.
	 */
	public scrollToBottom(): void
	{
		const bottom = window.outerHeight + 300;
		window.scrollTo(0, bottom);
	}

	/**
	 * Subscribes to the window scroll event and notifies Blazor when scrolled near the top.
	 * Safe to call repeatedly — tears down any existing listener before registering.
	 * @param blazorInteropUtility The Blazor interop utility instance used for callbacks.
	 * @param threshold The Y-position threshold that triggers the callback.
	 */
	public initializeWindowScrolledTopAsync(blazorInteropUtility: any, threshold: number)
	{
		this.destroyWindowScrolledTopAsync();

		this.blazorInteropUtility = blazorInteropUtility;

		this.boundScrollTopFunction = this.windowScrolledTopAsync.bind(this, threshold);

		window.addEventListener("scroll", this.boundScrollTopFunction);
	}

	/**
	 * Unsubscribes the previously registered window scroll handler.
	 */
	public destroyWindowScrolledTopAsync()
	{
		window.removeEventListener("scroll", this.boundScrollTopFunction);
	}

	/**
	 * Debounced scroll handler that notifies Blazor when window scroll position is below threshold.
	 * @param threshold The Y-position threshold used to trigger the callback.
	 */
	private async windowScrolledTopAsync(threshold: number)
	{
		// If there's a timer, cancel it
		if (this.scrollTimeout)
			window.clearTimeout(this.scrollTimeout);

		this.scrollTimeout = window.setTimeout(async () =>
		{
			if (window.scrollY < threshold)
				await this.blazorInteropUtility.invokeMethodAsync("OnWindowScrolledTopAsync");
		}, 100);
	}
}
