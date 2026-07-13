// Global Ctrl/Cmd+K handler for the command palette. Runs in the capture phase so the
// palette opens even while a text field has focus, without swallowing ordinary typing:
// only the Ctrl/Cmd+K chord is intercepted, everything else passes through untouched.

let dotNetRef = null;
let savedElement = null;

function onKeyDown(event) {
    if ((event.ctrlKey || event.metaKey) && (event.key === "k" || event.key === "K")) {
        event.preventDefault();
        dotNetRef?.invokeMethodAsync("OpenFromJs");
    }
}

export function register(ref) {
    dotNetRef = ref;
    document.addEventListener("keydown", onKeyDown, true);
}

export function dispose() {
    document.removeEventListener("keydown", onKeyDown, true);
    dotNetRef = null;
    savedElement = null;
}

// Remember where focus was before the palette opened so Esc can return it there.
export function saveFocus() {
    savedElement = document.activeElement;
}

export function restoreFocus() {
    if (savedElement && typeof savedElement.focus === "function") {
        savedElement.focus();
    }
    savedElement = null;
}
