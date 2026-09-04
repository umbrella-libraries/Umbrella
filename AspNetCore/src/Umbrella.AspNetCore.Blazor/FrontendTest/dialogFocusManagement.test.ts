// @vitest-environment jsdom

import { afterEach, describe, expect, it, vi } from "vitest";
import { UmbrellaBlazorInterop } from "../Content/scripts/blazor/index";

type DialogTestContext = {
	dotNetObjectReference: { invokeMethodAsync: ReturnType<typeof vi.fn> };
	interop: UmbrellaBlazorInterop;
};

const activeContexts = new Array<DialogTestContext>();

afterEach(() =>
{
	for (const context of activeContexts.splice(0))
	{
		context.interop.disposeDialog("test-dialog");
		context.interop.disposeDialogHost();
	}

	vi.restoreAllMocks();
	document.body.replaceChildren();
});

describe("dialog keyboard and focus management", () =>
{
	it("allows a nested control to consume Escape before the dialog handles it", async () =>
	{
		const context = initializeDialog("<button id=\"nested-control\">Open menu</button>");
		const nestedControl = getElement("nested-control");
		const consumeEscape = (event: KeyboardEvent): void => event.preventDefault();
		nestedControl.addEventListener("keydown", consumeEscape);

		const consumedEvent = new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true });
		nestedControl.dispatchEvent(consumedEvent);

		expect(consumedEvent.defaultPrevented).toBe(true);
		expect(context.dotNetObjectReference.invokeMethodAsync).not.toHaveBeenCalled();

		nestedControl.removeEventListener("keydown", consumeEscape);

		const unconsumedEvent = new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true });
		nestedControl.dispatchEvent(unconsumedEvent);
		await Promise.resolve();

		expect(unconsumedEvent.defaultPrevented).toBe(true);
		expect(context.dotNetObjectReference.invokeMethodAsync).toHaveBeenCalledOnce();
		expect(context.dotNetObjectReference.invokeMethodAsync).toHaveBeenCalledWith("CancelFromKeyboardAsync");
	});

	it("excludes negative-tabindex controls from initial focus and Tab boundaries", () =>
	{
		initializeDialog(`
			<button id="programmatic-only" tabindex="-1">Programmatic only</button>
			<button id="sequential">Sequential</button>`);

		const sequential = getElement("sequential");
		expect(document.activeElement).toBe(sequential);

		const tabEvent = new KeyboardEvent("keydown", { key: "Tab", bubbles: true, cancelable: true });
		sequential.dispatchEvent(tabEvent);

		expect(tabEvent.defaultPrevented).toBe(true);
		expect(document.activeElement).toBe(sequential);
	});

	it("still honors an explicit autofocus target with a negative tabindex", () =>
	{
		initializeDialog(`
			<button id="autofocus-target" tabindex="-1" autofocus>Programmatic autofocus</button>
			<button id="sequential">Sequential</button>`);

		expect(document.activeElement).toBe(getElement("autofocus-target"));
	});
});

function initializeDialog(content: string): DialogTestContext
{
	document.body.innerHTML = `
		<button id="invoker">Open dialog</button>
		<div id="backdrop"></div>
		<div id="dialog" role="dialog" tabindex="-1">${content}</div>`;

	vi.spyOn(HTMLElement.prototype, "getClientRects").mockReturnValue([{}] as unknown as DOMRectList);
	vi.spyOn(window, "requestAnimationFrame").mockImplementation(callback =>
	{
		callback(0);
		return 1;
	});

	getElement("invoker").focus();

	const interop = new UmbrellaBlazorInterop();
	interop.initializeDialogHost();

	const dotNetObjectReference = { invokeMethodAsync: vi.fn().mockResolvedValue(undefined) };
	const dialog = getElement("dialog");
	const backdrop = getElement("backdrop");
	interop.initializeDialog(dialog, backdrop, "test-dialog", dotNetObjectReference);

	const context = { dotNetObjectReference, interop };
	activeContexts.push(context);

	return context;
}

function getElement(id: string): HTMLElement
{
	const element = document.getElementById(id);

	if (!element)
		throw new Error(`Element '${id}' was not found.`);

	return element;
}
