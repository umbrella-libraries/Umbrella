/** Crop around the focal point, clamping the crop rectangle to the image edges. */
export function focalCropRectangle(sourceWidth: number, sourceHeight: number, width: number, height: number, x = 0.5, y = 0.5)
{
	const ratio = width / height;
	const cropWidth = Math.min(sourceWidth, sourceHeight * ratio);
	const cropHeight = Math.min(sourceHeight, sourceWidth / ratio);
	return {
		x: Math.max(0, Math.min(x * sourceWidth - cropWidth / 2, sourceWidth - cropWidth)),
		y: Math.max(0, Math.min(y * sourceHeight - cropHeight / 2, sourceHeight - cropHeight)),
		width: cropWidth,
		height: cropHeight
	};
}

const previewSubscriptions = new WeakMap<HTMLCanvasElement, AbortController>();

/** Reuses the decoded selector image; never requests a focal-crop URL. */
export function updateFocalPointPreview(selector: HTMLElement, canvas: HTMLCanvasElement, width: number, height: number, x: number | null, y: number | null): void
{
	previewSubscriptions.get(canvas)?.abort();
	const subscription = new AbortController();
	previewSubscriptions.set(canvas, subscription);
	const image = selector.querySelector("img");
	const context = canvas.getContext("2d");
	if (!image || !context || width <= 0 || height <= 0)
		return;

	context.clearRect(0, 0, canvas.width, canvas.height);
	const draw = () =>
	{
		if (!image.naturalWidth || !image.naturalHeight)
			return;

		const crop = focalCropRectangle(image.naturalWidth, image.naturalHeight, width, height, x ?? 0.5, y ?? 0.5);
		// The ScaleDown source may be smaller than the final crop. Avoid upscaling the preview bitmap.
		const scale = Math.min(1, crop.width / width, crop.height / height);
		canvas.width = Math.max(1, Math.round(width * scale));
		canvas.height = Math.max(1, Math.round(height * scale));
		context.drawImage(image, crop.x, crop.y, crop.width, crop.height, 0, 0, canvas.width, canvas.height);
	};
	image.addEventListener("load", draw, { signal: subscription.signal });
	if (image.complete)
		draw();
}
