(function () {
    const root = document.documentElement;

    function updateToggle(theme) {
        const isDark = theme === 'dark';
        document.querySelectorAll('.js-theme-toggle').forEach(btn => {
            btn.setAttribute('aria-pressed', String(isDark));
            const icon = btn.querySelector('i.js-theme-icon') || btn.querySelector('i');
            const text = btn.querySelector('.js-theme-text');
            if (icon) icon.className = isDark ? 'bi bi-moon-stars me-1 js-theme-icon' : 'bi bi-brightness-high me-1 js-theme-icon';
            if (text) text.textContent = isDark ? 'Dark' : 'Light';
        });
    }

    function setThemeDom(theme) {
        root.setAttribute('data-bs-theme', theme);
        updateToggle(theme);
    }

    async function saveThemeToServer(theme) {
        // theme hier: "dark" | "light"
        try {
            await fetch('/Settings/ThemeToggle', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ theme }) // { theme: "dark" | "light" }
            });
        } catch (err) {
            console.error('Failed to save theme to server', err);
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        // initial kommt von Server (DB/Cookie) über data-bs-theme
        const initial = root.getAttribute('data-bs-theme') || 'light';
        updateToggle(initial);

        // Attach listeners to all toggles (desktop + mobile)
        document.querySelectorAll('.js-theme-toggle').forEach(btn => {
            btn.addEventListener('click', () => {
                const current = root.getAttribute('data-bs-theme') || 'light';
                const next = current === 'dark' ? 'light' : 'dark';

                // 1. UI sofort umschalten
                setThemeDom(next);

                // 2. Sofort lokal persistieren für ausgeloggte Nutzer (und als Fallback)
                try {
                    localStorage.setItem('theme', next);
                } catch (e) { /* ignore */ }
                // zusätzlich Cookie setzen (wird vom Server beim eingeloggten Nutzer ebenfalls gesetzt)
                try {
                    document.cookie = 'theme=' + next + '; path=/; max-age=' + (60*60*24*365) + '; SameSite=Lax';
                } catch (e) { /* ignore */ }

                // 3. Server/DB im Hintergrund anpassen (versuchen, aber kein Must-Have)
                saveThemeToServer(next);
            });
        });
    });
})();
