// Light/dark theme toggle for RustArchon.
//
// The actual theme value lives on <html data-bs-theme="..."> (Bootstrap 5.3's own mechanism -
// app.css's variable overrides key off that same attribute, see wwwroot/app.css). This file is
// only responsible for: reading the stored preference, applying it, persisting a change, and
// wiring up whatever toggle button is present on the current page. The anti-flash inline script
// in App.razor's <head> duplicates just the "read + apply" half so the correct theme is set
// before first paint, before this file has even loaded.
(function () {
    "use strict";

    const STORAGE_KEY = "rustarchon-theme";

    function getStoredTheme() {
        try {
            return localStorage.getItem(STORAGE_KEY);
        } catch {
            return null;
        }
    }

    function setStoredTheme(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch {
            // Private browsing / storage disabled - the toggle still works for this page load,
            // it just won't be remembered next visit.
        }
    }

    function currentTheme() {
        return document.documentElement.getAttribute("data-bs-theme") === "light" ? "light" : "dark";
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute("data-bs-theme", theme);
        document.querySelectorAll("[data-theme-icon]").forEach((el) => {
            el.textContent = theme === "dark" ? "☀" : "◐"; // sun / half-circle
        });
        document.querySelectorAll("[data-theme-toggle]").forEach((el) => {
            el.setAttribute("aria-pressed", theme === "dark" ? "true" : "false");
        });
    }

    function toggleTheme() {
        const next = currentTheme() === "dark" ? "light" : "dark";
        setStoredTheme(next);
        applyTheme(next);
    }

    // Blazor Server re-renders pieces of the DOM (including the toggle button) as the user
    // navigates, without a full page reload - a single top-level listener that ignores clicks
    // outside the toggle survives those re-renders, unlike per-element addEventListener calls
    // which would need to be re-attached after every render.
    document.addEventListener("click", function (event) {
        if (event.target.closest("[data-theme-toggle]")) {
            toggleTheme();
        }
    });

    // Reflect whatever the anti-flash script already applied (icon state, aria-pressed) once the
    // DOM is actually available to query - the inline script runs before <body> exists.
    document.addEventListener("DOMContentLoaded", function () {
        applyTheme(currentTheme());
    });
})();
