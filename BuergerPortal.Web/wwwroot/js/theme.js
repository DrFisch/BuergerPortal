(function () {
    function setTheme(next) {
        document.documentElement.setAttribute('data-bs-theme', next);
        localStorage.setItem('theme', next);
    }

    // Buttons: überall wo .js-theme-toggle gesetzt ist
    const bind = () => {
        document.querySelectorAll('.js-theme-toggle').forEach(btn => {
            btn.addEventListener('click', () => {
                const current = document.documentElement.getAttribute('data-bs-theme') || 'light';
                const next = current === 'dark' ? 'light' : 'dark';
                setTheme(next);
            });
        });
    };

    // initial label optional aktualisieren (falls du später Text ändern willst)
    document.addEventListener('DOMContentLoaded', bind);
})();
