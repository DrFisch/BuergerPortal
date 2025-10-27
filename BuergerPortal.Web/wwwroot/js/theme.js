// wwwroot/js/theme.js
(function () {
    const root = document.documentElement;
    const KEY = 'theme';

    function setTheme(theme) {
        root.setAttribute('data-bs-theme', theme);
        localStorage.setItem(KEY, theme);
        updateToggle(theme);
    }
    function getTheme() {
        return root.getAttribute('data-bs-theme') || 'light';
    }
    function updateToggle(theme) {
        const btn = document.querySelector('.js-theme-toggle');
        if (!btn) return;
        const isDark = theme === 'dark';
        btn.setAttribute('aria-pressed', String(isDark));
        const icon = btn.querySelector('i');
        const text = btn.querySelector('.js-theme-text');
        // Icon/Text zeigen den AKTUELLEN Zustand
        if (icon) icon.className = isDark ? 'bi bi-moon-stars me-1' : 'bi bi-brightness-high me-1';
        if (text) text.textContent = isDark ? 'Dark' : 'Light';
    }

    document.addEventListener('DOMContentLoaded', () => {
        updateToggle(window.__initialTheme || getTheme());
        document.querySelector('.js-theme-toggle')?.addEventListener('click', () => {
            setTheme(getTheme() === 'dark' ? 'light' : 'dark');
        });
    });
})();
