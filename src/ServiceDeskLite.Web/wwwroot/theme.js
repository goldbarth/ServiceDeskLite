// Theme switching for the design tokens: the `sdl-dark` class on <html> selects the
// dark block of DesignTokens.RootCss. MudBlazor's palette follows separately via
// MudThemeProvider.IsDarkMode; MainLayout keeps both in sync through these functions.
window.sdlTheme = {
    // Stored choice wins; first visit follows the OS preference.
    isDark: function () {
        const stored = localStorage.getItem("sdl-theme");
        if (stored === "dark" || stored === "light") {
            return stored === "dark";
        }
        return window.matchMedia("(prefers-color-scheme: dark)").matches;
    },

    apply: function (dark) {
        document.documentElement.classList.toggle("sdl-dark", dark);
        localStorage.setItem("sdl-theme", dark ? "dark" : "light");
    }
};
