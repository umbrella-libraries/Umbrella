import "quill/dist/quill.bubble.css";
import "quill/dist/quill.snow.css";
import { TextEditorInterop } from "./blazor/textEditor";

declare global
{
	interface Window
	{
		UmbrellaBlazorTextEditorInterop: TextEditorInterop;
	}
}

(() =>
{
	if (!window.UmbrellaBlazorTextEditorInterop)
		window.UmbrellaBlazorTextEditorInterop = new TextEditorInterop();
})();
