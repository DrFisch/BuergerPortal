// Hinweis-Dialog vor dem Wechsel zur BundID-Simulation (Studienprojekt).
// Ein Klick auf einen Anmelde-Link (/Account/Login) oder auf ein Element mit data-sim-hinweis="login|stepup|postfach"
// öffnet den Dialog (Views/Shared/_SimulationsHinweis.cshtml). Erst „Weiter“ führt das eigentliche Ziel aus:
// Links werden aufgerufen, Formular-Knöpfe senden ihr Formular (mit ihrem name/value) ab.
(function () {
    const modalEl = document.getElementById('simHinweisModal');
    if (!modalEl || !window.bootstrap) {
        return; // ohne Dialog oder Bootstrap bleibt alles wie bisher
    }
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const weiter = modalEl.querySelector('[data-sim-weiter]');
    let ausfuehren = null;

    function hinweisTyp(el) {
        if (el.dataset.simHinweis) {
            return el.dataset.simHinweis;
        }
        if (el.tagName === 'A' && el.href) {
            const url = new URL(el.href, window.location.href);
            if (url.origin === window.location.origin && url.pathname.toLowerCase() === '/account/login') {
                return 'login';
            }
        }
        return null;
    }

    function zeigen(typ, ziel) {
        modalEl.querySelectorAll('[data-sim-variante]').forEach(function (block) {
            block.hidden = !block.dataset.simVariante.split(' ').includes(typ);
        });
        ausfuehren = ziel;
        modal.show();
    }

    document.addEventListener('click', function (e) {
        // Neuer Tab / neues Fenster (Strg, Umschalt, mittlere Taste) bleibt unverändert.
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) {
            return;
        }
        const el = e.target.closest('a[href], button[data-sim-hinweis]');
        if (!el || modalEl.contains(el)) {
            return;
        }
        const typ = hinweisTyp(el);
        if (!typ) {
            return;
        }
        e.preventDefault();
        if (el.tagName === 'A') {
            zeigen(typ, function () { window.location.href = el.href; });
        } else if (el.form) {
            zeigen(typ, function () {
                if (el.form.requestSubmit) {
                    el.form.requestSubmit(el);
                } else {
                    el.form.submit();
                }
            });
        }
    });

    weiter.addEventListener('click', function () {
        const ziel = ausfuehren;
        ausfuehren = null;
        modal.hide();
        if (ziel) {
            ziel();
        }
    });

    modalEl.addEventListener('hidden.bs.modal', function () {
        ausfuehren = null;
    });
})();
