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
	window.UmbrellaBlazorInterop = new UmbrellaBlazorInterop();
})();