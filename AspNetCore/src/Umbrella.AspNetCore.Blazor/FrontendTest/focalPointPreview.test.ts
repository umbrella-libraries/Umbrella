// @vitest-environment jsdom
import { describe, expect, it, vi } from "vitest";
import { focalCropRectangle, updateFocalPointPreview } from "../Content/scripts/blazor/focalPointPreview";

describe("local focal preview", () =>
{
	it("centers and clamps landscape and portrait crops", () =>
	{
		expect(focalCropRectangle(400, 200, 100, 100)).toEqual({ x: 100, y: 0, width: 200, height: 200 });
		expect(focalCropRectangle(400, 200, 100, 100, 0, 0)).toEqual({ x: 0, y: 0, width: 200, height: 200 });
		expect(focalCropRectangle(400, 200, 100, 100, 1, 1)).toEqual({ x: 200, y: 0, width: 200, height: 200 });
		expect(focalCropRectangle(200, 400, 100, 100, 1, 1)).toEqual({ x: 0, y: 200, width: 200, height: 200 });
	});
	it("draws the existing image and replaces pending load callbacks with the latest selection", () =>
	{
		const selector = document.createElement("div");
		const image = document.createElement("img");
		image.src = "/dynamicimage/400/200/ScaleDown/jpg/image.jpg";
		Object.defineProperties(image, { naturalWidth: { value: 400 }, naturalHeight: { value: 200 }, complete: { value: false } });
		selector.append(image);
		const canvas = document.createElement("canvas");
		const drawImage = vi.fn();
		vi.spyOn(canvas, "getContext").mockReturnValue({ drawImage, clearRect: vi.fn() } as unknown as CanvasRenderingContext2D);
		updateFocalPointPreview(selector, canvas, 100, 100, 0, 0);
		updateFocalPointPreview(selector, canvas, 100, 100, 1, 1);
		image.dispatchEvent(new Event("load"));
		expect(drawImage).toHaveBeenCalledExactlyOnceWith(image, 200, 0, 200, 200, 0, 0, 100, 100);
		expect(image.getAttribute("src")).toBe("/dynamicimage/400/200/ScaleDown/jpg/image.jpg");
	});
});
