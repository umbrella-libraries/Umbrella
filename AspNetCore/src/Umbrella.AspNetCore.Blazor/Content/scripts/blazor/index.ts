/* eslint-disable */
import { BrowserEventAggregator } from "./browserEventAggregator";
import { updateFocalPointPreview } from "./focalPointPreview";

const dialogFocusableSelector = [
	"a[href]",
	"area[href]",
	"button:not([disabled])",
	"input:not([disabled]):not([type=\"hidden\"])",
	"select:not([disabled])",
	"textarea:not([disabled])",
	"iframe",
	"object",
	"embed",
	"summary",
	"audio[controls]",
	"video[controls]",
	"[contenteditable=\"true\"]",
	"[tabindex]:not([tabindex=\"-1\"])"
].join(",");

type DialogDotNetObjectReference = {
	invokeMethodAsync: (methodName: "CancelFromKeyboardAsync") => Promise<void>;
};

type DialogRegistration = {
	id: string;
	surface: HTMLElement;
	backdrop: HTMLElement;
	invoker: HTMLElement | null;
	dotNetObjectReference: DialogDotNetObjectReference;
	cancelPending: boolean;
};

/**
 * Provides JavaScript interop helpers used by Blazor components.
 */
export class UmbrellaBlazorInterop
{
	#browserEventAggregator: BrowserEventAggregator | null = null;
	#imageFocalPointSelectors = new WeakSet<HTMLElement>();
	#dialogs = new Array<DialogRegistration>();
	#backgroundInertState = new Map<HTMLElement, boolean>();
	#dialogHostCount = 0;
	#lastFocusedOutsideDialogs: HTMLElement | null = null;
	#dialogListenersAttached = false;
	#dialogTabKeyDownHandler = (event: KeyboardEvent): void => this.handleDialogTabKeyDown(event);
	#dialogEscapeKeyDownHandler = (event: KeyboardEvent): void => this.handleDialogEscapeKeyDown(event);
	#dialogFocusInHandler = (event: FocusEvent): void => this.handleDialogFocusIn(event);

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
	 * Gets the visible bounds of the first image contained by an element.
	 * @param element The focal-point selector containing the image.
	 */
	public getImageBounds(element: HTMLElement): { left: number; top: number; width: number; height: number }
	{
		const target = element.querySelector("img") ?? element;
		const bounds = target.getBoundingClientRect();

		return {
			left: bounds.left,
			top: bounds.top,
			width: bounds.width,
			height: bounds.height
		};
	}

	/**
	 * Prevents handled focal-point arrow keys from also scrolling the document.
	 * @param element The interactive focal-point selector.
	 */
	public initializeImageFocalPointSelector(element: HTMLElement): void
	{
		if (this.#imageFocalPointSelectors.has(element))
			return;

		element.addEventListener("keydown", event =>
		{
			if (event.key === "ArrowLeft" ||
				event.key === "ArrowRight" ||
				event.key === "ArrowUp" ||
				event.key === "ArrowDown")
			{
				event.preventDefault();
			}
		});
		this.#imageFocalPointSelectors.add(element);
	}

	/** Updates a local preview without making image requests. */
	public updateImageFocalPointPreview(selector: HTMLElement, canvas: HTMLCanvasElement, width: number, height: number, x: number | null, y: number | null): void
	{
		updateFocalPointPreview(selector, canvas, width, height, x, y);
	}

	/**
	 * Starts tracking the element that opened a dialog before dialog markup is rendered.
	 * Safe to call once for each dialog host on the page.
	 */
	public initializeDialogHost(): void
	{
		this.#dialogHostCount++;
		this.#lastFocusedOutsideDialogs = this.getActiveHtmlElement();
		this.attachDialogListeners();
	}

	/**
	 * Stops focus tracking for a dialog host once no hosts or dialogs remain.
	 */
	public disposeDialogHost(): void
	{
		this.#dialogHostCount = Math.max(0, this.#dialogHostCount - 1);
		this.detachDialogListenersWhenUnused();
	}

	/**
	 * Registers a rendered dialog, makes it the active modal, and moves focus into it.
	 * @param surface The dialog surface.
	 * @param backdrop The dialog backdrop.
	 * @param id The stable identifier assigned by the Blazor component.
	 * @param dotNetObjectReference The callback target used to cancel the active dialog with Escape.
	 */
	public initializeDialog(surface: HTMLElement, backdrop: HTMLElement, id: string, dotNetObjectReference: DialogDotNetObjectReference): void
	{
		if (this.#dialogs.some(x => x.id === id))
			return;

		const activeElement = this.getActiveHtmlElement();
		const invoker = activeElement && !surface.contains(activeElement) && !backdrop.contains(activeElement)
			? activeElement
			: this.#lastFocusedOutsideDialogs;

		this.#dialogs.push({
			id,
			surface,
			backdrop,
			invoker,
			dotNetObjectReference,
			cancelPending: false
		});

		this.attachDialogListeners();
		this.synchronizeDialogs();
		window.requestAnimationFrame(() =>
		{
			const registration = this.getActiveDialog();

			if (registration?.id === id)
				this.focusInitialElement(registration);
		});
	}

	/**
	 * Unregisters a dialog, restores the previous modal state, and returns focus to its invoker.
	 * @param id The identifier assigned when the dialog was initialized.
	 */
	public disposeDialog(id: string): void
	{
		const index = this.#dialogs.findIndex(x => x.id === id);

		if (index < 0)
			return;

		const wasActive = index === this.#dialogs.length - 1;
		const [registration] = this.#dialogs.splice(index, 1);

		if (!registration)
			return;

		registration.surface.inert = false;
		registration.backdrop.inert = false;
		registration.surface.removeAttribute("aria-hidden");
		this.synchronizeDialogs();

		if (wasActive)
		{
			const activeDialog = this.getActiveDialog();

			if (this.canRestoreFocus(registration.invoker, activeDialog))
				registration.invoker.focus({ preventScroll: true });
			else if (activeDialog)
				this.focusInitialElement(activeDialog);
		}

		this.detachDialogListenersWhenUnused();
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

	private attachDialogListeners(): void
	{
		if (this.#dialogListenersAttached)
			return;

		document.addEventListener("keydown", this.#dialogTabKeyDownHandler, true);
		window.addEventListener("keydown", this.#dialogEscapeKeyDownHandler);
		document.addEventListener("focusin", this.#dialogFocusInHandler, true);
		this.#dialogListenersAttached = true;
	}

	private detachDialogListenersWhenUnused(): void
	{
		if (!this.#dialogListenersAttached || this.#dialogs.length > 0 || this.#dialogHostCount > 0)
			return;

		document.removeEventListener("keydown", this.#dialogTabKeyDownHandler, true);
		window.removeEventListener("keydown", this.#dialogEscapeKeyDownHandler);
		document.removeEventListener("focusin", this.#dialogFocusInHandler, true);
		this.#dialogListenersAttached = false;
		this.#lastFocusedOutsideDialogs = null;
	}

	private handleDialogEscapeKeyDown(event: KeyboardEvent): void
	{
		if (event.key !== "Escape" || event.defaultPrevented)
			return;

		const activeDialog = this.getActiveDialog();

		if (!activeDialog)
			return;

		event.preventDefault();
		event.stopPropagation();

		if (!activeDialog.cancelPending)
		{
			activeDialog.cancelPending = true;
			activeDialog.dotNetObjectReference.invokeMethodAsync("CancelFromKeyboardAsync")
				.catch((error: unknown) =>
				{
					activeDialog.cancelPending = false;
					console.error("Failed to cancel the active dialog.", error);
				});
		}
	}

	private handleDialogTabKeyDown(event: KeyboardEvent): void
	{
		if (event.key !== "Tab")
			return;

		const activeDialog = this.getActiveDialog();

		if (!activeDialog)
			return;

		const focusableElements = this.getTabbableElements(activeDialog.surface);

		if (focusableElements.length === 0)
		{
			event.preventDefault();
			activeDialog.surface.focus({ preventScroll: true });
			return;
		}

		const first = focusableElements[0];
		const last = focusableElements[focusableElements.length - 1];
		const activeElement = this.getActiveHtmlElement();

		if (!first || !last)
			return;

		if (event.shiftKey && (activeElement === first || !activeDialog.surface.contains(activeElement)))
		{
			event.preventDefault();
			last.focus({ preventScroll: true });
		}
		else if (!event.shiftKey && (activeElement === last || !activeDialog.surface.contains(activeElement)))
		{
			event.preventDefault();
			first.focus({ preventScroll: true });
		}
	}

	private handleDialogFocusIn(event: FocusEvent): void
	{
		const target = event.target instanceof HTMLElement ? event.target : null;
		const activeDialog = this.getActiveDialog();

		if (!activeDialog)
		{
			if (target && !target.closest("[data-umbrella-dialog-id]"))
				this.#lastFocusedOutsideDialogs = target;

			return;
		}

		if (!target || !activeDialog.surface.contains(target))
			this.focusInitialElement(activeDialog);
	}

	private synchronizeDialogs(): void
	{
		this.restoreBackgroundInertState();

		const activeDialog = this.getActiveDialog();

		for (const registration of this.#dialogs)
		{
			const isActive = registration === activeDialog;
			registration.surface.inert = !isActive;
			registration.backdrop.inert = !isActive;

			if (isActive)
				registration.surface.removeAttribute("aria-hidden");
			else
				registration.surface.setAttribute("aria-hidden", "true");
		}

		if (!activeDialog)
			return;

		const dialogElements = new Set(this.#dialogs.flatMap(x => [x.surface, x.backdrop]));
		let current: HTMLElement | null = activeDialog.surface;

		while (current && current !== document.body)
		{
			const parent: HTMLElement | null = current.parentElement;

			if (!parent)
				break;

			for (const sibling of Array.from(parent.children))
			{
				if (sibling instanceof HTMLElement && sibling !== current && !dialogElements.has(sibling))
				{
					this.#backgroundInertState.set(sibling, sibling.inert);
					sibling.inert = true;
				}
			}

			current = parent;
		}
	}

	private restoreBackgroundInertState(): void
	{
		for (const [element, wasInert] of this.#backgroundInertState)
		{
			if (element.isConnected)
				element.inert = wasInert;
		}

		this.#backgroundInertState.clear();
	}

	private focusInitialElement(registration: DialogRegistration): void
	{
		const autofocusElement = registration.surface.querySelector<HTMLElement>("[autofocus]");
		const focusTarget = autofocusElement && this.isFocusable(autofocusElement)
			? autofocusElement
			: this.getTabbableElements(registration.surface)[0] ?? registration.surface;

		focusTarget.focus({ preventScroll: true });
	}

	private getTabbableElements(surface: HTMLElement): HTMLElement[]
	{
		return Array.from(surface.querySelectorAll<HTMLElement>(dialogFocusableSelector)).filter(x => x.tabIndex >= 0 && this.isFocusable(x));
	}

	private isFocusable(element: HTMLElement): boolean
	{
		if (element.matches(":disabled") || element.closest("[inert], [aria-hidden=\"true\"]"))
			return false;

		const style = window.getComputedStyle(element);

		return style.display !== "none" && style.visibility !== "hidden" && element.getClientRects().length > 0;
	}

	private canRestoreFocus(element: HTMLElement | null, activeDialog: DialogRegistration | undefined): element is HTMLElement
	{
		if (!element?.isConnected || element.closest("[inert]"))
			return false;

		return !activeDialog || activeDialog.surface.contains(element);
	}

	private getActiveDialog(): DialogRegistration | undefined
	{
		return this.#dialogs[this.#dialogs.length - 1];
	}

	private getActiveHtmlElement(): HTMLElement | null
	{
		return document.activeElement instanceof HTMLElement ? document.activeElement : null;
	}
}
