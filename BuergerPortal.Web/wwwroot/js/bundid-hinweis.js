// Hinweis-Dialog vor dem Wechsel zur BundID-Simulation (Studienprojekt).
// Ein Klick auf einen Anmelde-Link (/Account/Login) oder auf ein Element mit data-sim-hinweis="login|stepup|postfach"
// öffnet den Dialog (Views/Shared/_SimulationsHinweis.cshtml). Erst „Weiter“ führt das eigentliche Ziel aus:
// Links werden aufgerufen, Formular-Knöpfe senden ihr Formular (mit ihrem name/value) ab.
// „Schritt für Schritt ansehen“ (Lernpfad) setzt vorher ein kurzlebiges Cookie; der OIDC-Handler des Portals liest es
// beim Start der Anmeldung und zeigt dann jeden Schritt als Erklärseite (Services/Lernpfad.cs).
(function () {
    const modalEl = document.getElementById('simHinweisModal');
    if (!modalEl || !window.bootstrap) {
        return; // ohne Dialog oder Bootstrap bleibt alles wie bisher
    }
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const weiter = modalEl.querySelector('[data-sim-weiter]');
    const lernpfad = modalEl.querySelector('[data-sim-lernpfad]');

    // 10 Minuten gültig, nur für dieses Portal (__Host-: Secure, Path=/, ohne Domain)
    function lernpfadCookie(an) {
        document.cookie = '__Host-bp-lernpfad=' + (an ? '1' : '') + '; Path=/; Max-Age=' + (an ? 600 : 0) +
            '; Secure; SameSite=Lax';
    }
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
        // „Was dort passiert“ / „In echt“: am Computer offen, auf dem Handy zugeklappt (Dialog sonst bildschirmfüllend)
        const breit = !window.matchMedia || window.matchMedia('(min-width: 768px)').matches;
        modalEl.querySelectorAll('[data-sim-mehr]').forEach(function (mehr) {
            mehr.open = breit;
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

    function starten(mitLernpfad) {
        const ziel = ausfuehren;
        ausfuehren = null;
        lernpfadCookie(mitLernpfad);
        modal.hide();
        if (ziel) {
            ziel();
        }
    }

    weiter.addEventListener('click', function () { starten(false); });
    if (lernpfad) {
        lernpfad.addEventListener('click', function () { starten(true); });
    }

    modalEl.addEventListener('hidden.bs.modal', function () {
        ausfuehren = null;
    });
})();
