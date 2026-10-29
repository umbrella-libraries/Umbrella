// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { compile } from "sass";
import path from "path";

const checkboxDirectory = path.join(__dirname, "..", "Components", "Checkbox");

function compileScopedSass(fileName: string): string
{
	return compile(path.join(checkboxDirectory, fileName), { loadPaths: ["node_modules"] }).css;
}

beforeEach(() =>
{
	const style = document.createElement("style");
	style.textContent = compileScopedSass("UmbrellaCheckbox.razor.scss") + compileScopedSass("UmbrellaCheckboxGroup.razor.scss");
	document.head.append(style);

	// Mirrors the markup rendered by UmbrellaCheckboxGroup and UmbrellaCheckbox.
	document.body.innerHTML = `
		<ul class="u-checkbox-group">
			<li class="u-checkbox-group__item">
				<div class="u-checkbox-group__checkbox-container">
					<div class="u-checkbox">
						<input type="checkbox" id="articles" class="u-checkbox__input" />
						<label for="articles" class="u-checkbox__label">Articles</label>
					</div>
					<button type="button" class="u-checkbox-group__collapse-toggle" title="Hide" aria-expanded="true">
						<span class="u-checkbox-group__collapse-toggle-text">Hide</span>
					</button>
				</div>
			</li>
		</ul>`;
});

afterEach(() =>
{
	document.head.replaceChildren();
	document.body.replaceChildren();
});

describe("checkbox accessibility", () =>
{
	it("keeps the native checkbox input in the accessibility tree and the tab order", () =>
	{
		const input = document.querySelector<HTMLInputElement>(".u-checkbox__input")!;
		const style = getComputedStyle(input);

		expect(style.display).not.toBe("none");
		expect(style.visibility).not.toBe("hidden");
		expect(style.opacity).toBe("0");
		expect(style.position).toBe("absolute");

		input.focus();
		expect(document.activeElement).toBe(input);
	});

	it("shows a focus indicator on the custom box when the input has keyboard focus", () =>
	{
		const css = compileScopedSass("UmbrellaCheckbox.razor.scss");

		expect(css).toMatch(/\.u-checkbox__input:focus-visible \+ label::before\s*\{[^}]*outline:/);
	});

	it("keeps the collapse toggle text available as the button's accessible name", () =>
	{
		const text = document.querySelector<HTMLElement>(".u-checkbox-group__collapse-toggle-text")!;
		const style = getComputedStyle(text);

		expect(style.display).not.toBe("none");
		expect(style.visibility).not.toBe("hidden");
	});
});
