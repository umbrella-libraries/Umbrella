type SavedStyle = { element: HTMLElement; property: string; value: string; priority: string };

/** Owns only the styles needed to freeze the page behind an active modal. */
export class DialogScrollLock
{
	#styles: SavedStyle[] | null = null;
	#scrollX = 0;
	#scrollY = 0;

	public lock(): void
	{
		if (this.#styles)
			return;

		const root = document.documentElement;
		const body = document.body;
		this.#scrollX = window.scrollX;
		this.#scrollY = window.scrollY;
		const scrollbarWidth = root.clientWidth > 0 ? Math.max(0, window.innerWidth - root.clientWidth) : 0;
		const paddingRight = Number.parseFloat(window.getComputedStyle(body).paddingRight) || 0;
		this.#styles = [];

		this.setStyle(root, "overflow-x", "hidden");
		this.setStyle(root, "overflow-y", "hidden");
		this.setStyle(root, "overscroll-behavior", "none");
		// A fixed body also prevents background touch scrolling on mobile Safari.
		this.setStyle(body, "position", "fixed");
		this.setStyle(body, "top", `${-this.#scrollY}px`);
		this.setStyle(body, "left", `${-this.#scrollX}px`);
		this.setStyle(body, "width", "100%");
		this.setStyle(body, "box-sizing", "border-box");
		this.setStyle(body, "overflow-x", "hidden");
		this.setStyle(body, "overflow-y", "hidden");
		if (scrollbarWidth > 0)
			this.setStyle(body, "padding-right", `${paddingRight + scrollbarWidth}px`);
	}

	public unlock(): void
	{
		if (!this.#styles)
			return;

		for (const { element, property, value, priority } of this.#styles.reverse())
		{
			if (value)
				element.style.setProperty(property, value, priority);
			else
				element.style.removeProperty(property);
		}

		this.#styles = null;
		if (window.scrollX !== this.#scrollX || window.scrollY !== this.#scrollY)
			window.scrollTo({ left: this.#scrollX, top: this.#scrollY, behavior: "instant" });
	}

	private setStyle(element: HTMLElement, property: string, value: string): void
	{
		this.#styles!.push({ element, property, value: element.style.getPropertyValue(property), priority: element.style.getPropertyPriority(property) });
		element.style.setProperty(property, value, "important");
	}
}
