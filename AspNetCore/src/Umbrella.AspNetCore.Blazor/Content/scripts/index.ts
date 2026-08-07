import { UmbrellaBlazorInterop } from "./blazor/index";

declare global
{
	interface Window
	{
		UmbrellaBlazorInterop: UmbrellaBlazorInterop;
	}
}

(() =>
{
	if (!window.UmbrellaBlazorInterop)
		window.UmbrellaBlazorInterop = new UmbrellaBlazorInterop();
})();
