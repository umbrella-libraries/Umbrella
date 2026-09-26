// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { UmbrellaBlazorInterop } from "../Content/scripts/blazor/index";

let interop: UmbrellaBlazorInterop;
let scrollTo: ReturnType<typeof vi.fn>;

beforeEach(() =>
{
	document.body.replaceChildren();
	document.body.removeAttribute("style");
	document.documentElement.removeAttribute("style");
	vi.spyOn(HTMLElement.prototype, "getClientRects").mockReturnValue([{}] as unknown as DOMRectList);
	vi.spyOn(window, "requestAnimationFrame").mockImplementation(callback =>
	{
		callback(0); return 1;
	});
	scrollTo = vi.fn();
	vi.spyOn(window, "scrollTo").mockImplementation(scrollTo);
	vi.stubGlobal("innerWidth", 1000);
	vi.spyOn(document.documentElement, "clientWidth", "get").mockReturnValue(980);
	interop = new UmbrellaBlazorInterop();
	interop.initializeDialogHost();
});

afterEach(() =>
{
	interop.disposeDialogHost();
	vi.restoreAllMocks();
	vi.unstubAllGlobals();
	document.body.replaceChildren();
	document.body.removeAttribute("style");
	document.documentElement.removeAttribute("style");
});

describe("dialog page scroll lock", () =>
{
	it("locks the page, compensates the scrollbar and leaves modal scrolling gestures alone", () =>
	{
		document.body.style.paddingRight = "7px";
		const surface = openDialog("first");
		expect(document.documentElement.style.overflowY).toBe("hidden");
		expect(document.body.style.position).toBe("fixed");
		expect(document.body.style.paddingRight).toBe("27px");
		const wheel = new WheelEvent("wheel", { bubbles: true, cancelable: true, deltaY: 100 });
		const touch = new Event("touchmove", { bubbles: true, cancelable: true });
		surface.dispatchEvent(wheel);
		surface.dispatchEvent(touch);
		expect(wheel.defaultPrevented).toBe(false);
		expect(touch.defaultPrevented).toBe(false);
	});

	it("restores position and owned styles including priorities without discarding unrelated changes", () =>
	{
		vi.stubGlobal("scrollX", 12);
		vi.stubGlobal("scrollY", 420);
		document.body.style.setProperty("position", "relative", "important");
		document.body.style.setProperty("padding-right", "7px", "important");
		document.documentElement.style.setProperty("overflow-y", "scroll", "important");
		openDialog("first");
		expect(document.body.style.top).toBe("-420px");
		expect(document.body.style.left).toBe("-12px");
		document.body.style.backgroundColor = "red";
		vi.stubGlobal("scrollX", 0);
		vi.stubGlobal("scrollY", 0);
		interop.disposeDialog("first");
		expect(document.body.style.position).toBe("relative");
		expect(document.body.style.getPropertyPriority("position")).toBe("important");
		expect(document.body.style.paddingRight).toBe("7px");
		expect(document.body.style.getPropertyPriority("padding-right")).toBe("important");
		expect(document.body.style.top).toBe("");
		expect(document.body.style.width).toBe("");
		expect(document.body.style.backgroundColor).toBe("red");
		expect(document.documentElement.style.overflowY).toBe("scroll");
		expect(document.documentElement.style.getPropertyPriority("overflow-y")).toBe("important");
		expect(scrollTo).toHaveBeenCalledWith({ left: 12, top: 420, behavior: "instant" });
	});

	it("does not add padding when the browser has overlay scrollbars", () =>
	{
		vi.spyOn(document.documentElement, "clientWidth", "get").mockReturnValue(1000);
		openDialog("first");
		expect(document.body.style.paddingRight).toBe("");
	});

	it("keeps one lock across stacked dialogs and releases it only after the last closes", () =>
	{
		openDialog("first");
		openDialog("second");
		expect(document.body.style.paddingRight).toBe("20px");
		interop.disposeDialog("second");
		expect(document.body.style.position).toBe("fixed");
		interop.disposeDialog("first");
		expect(document.body.style.position).toBe("");
		expect(document.body.style.paddingRight).toBe("");
	});

	it("handles out-of-order close and repeated disposal", () =>
	{
		openDialog("first");
		openDialog("second");
		interop.disposeDialog("first");
		interop.disposeDialog("first");
		expect(document.body.style.position).toBe("fixed");
		interop.disposeDialog("second");
		interop.disposeDialog("second");
		expect(document.body.style.position).toBe("");
	});

	it("does not register a duplicate dialog or double compensate the scrollbar", () =>
	{
		const surface = openDialog("first");
		interop.initializeDialog(surface, surface.previousElementSibling as HTMLElement, "first", { invokeMethodAsync: vi.fn().mockResolvedValue(undefined) });
		expect(document.body.style.paddingRight).toBe("20px");
		interop.disposeDialog("first");
		expect(document.body.style.position).toBe("");
	});

	it("releases the lock if navigation removes the dialog without normal disposal", async () =>
	{
		const surface = openDialog("first");
		surface.remove();
		await Promise.resolve();
		expect(document.body.style.position).toBe("");
	});

	it("does not release the lock for ordinary content updates inside a dialog", async () =>
	{
		const surface = openDialog("first");
		surface.append(document.createElement("button"));
		await Promise.resolve();
		expect(document.body.style.position).toBe("fixed");
	});

	it("cleans up on final host disposal but not while another host remains", () =>
	{
		interop.initializeDialogHost();
		openDialog("first");
		interop.disposeDialogHost();
		expect(document.body.style.position).toBe("fixed");
		interop.disposeDialogHost();
		expect(document.body.style.position).toBe("");
		expect(document.documentElement.style.overflowY).toBe("");
	});

	it("captures fresh scroll position after closing and reopening", () =>
	{
		openDialog("first");
		interop.disposeDialog("first");
		vi.stubGlobal("scrollY", 800);
		openDialog("second");
		expect(document.body.style.top).toBe("-800px");
	});
});

function openDialog(id: string): HTMLElement
{
	const backdrop = document.createElement("div");
	const surface = document.createElement("div");
	surface.tabIndex = -1;
	surface.setAttribute("role", "dialog");
	surface.dataset.umbrellaDialogId = id;
	document.body.append(backdrop, surface);
	interop.initializeDialog(surface, backdrop, id, { invokeMethodAsync: vi.fn().mockResolvedValue(undefined) });
	return surface;
}
