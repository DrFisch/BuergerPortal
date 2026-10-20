// Portal 2 – Verhalten des App-Rahmens (Views/Modern/Shared/_LayoutModern.cshtml). Ohne Bootstrap-JavaScript.
// 1. Dialoge: <dialog> statt Bootstrap-Modal. Die gemeinsamen Skripte beider Oberflächen (js/bundid-hinweis.js,
//    js/sitzung.js, pwa-install.js) rufen bootstrap.Modal.getOrCreateInstance(el).show()/hide() auf und hören auf
//    "hidden.bs.modal" – genau diese kleine Schnittstelle bildet window.bootstrap hier für <dialog> nach. So bleibt das
//    Verhalten in beiden Oberflächen gleich, ohne die Skripte zu verdoppeln.
// 2. Blätter: [data-m-blatt="#id"] öffnet ein <dialog>, [data-m-zu] und [data-bs-dismiss="modal"] schließen es,
//    ein Tipp auf den abgedunkelten Hintergrund auch (außer bei data-m-fest, z. B. Sitzungswarnung).
// 3. Großer Titel: Scrollt er aus dem Bild, erscheint er klein in der Kopfzeile (wie iOS/Android).
// 4. Toast: window.mToast(text) zeigt kurz eine Meldung unten.
// 5. Rückfrage vor Formularen mit data-confirm. 6. Segment-Filter [data-m-filter] (Gruppen [data-gruppe]).
(function () {
    'use strict';

    // ---------- 1. Dialoge ----------
    const instanzen = new WeakMap();

    function instanz(el) {
        let i = instanzen.get(el);
        if (!i) {
            i = {
                show() {
                    if (el.open || typeof el.showModal !== 'function') return;
                    el.showModal();
                    el.dispatchEvent(new Event('shown.bs.modal'));
                },
                hide() {
                    if (el.open) el.close();
                }
            };
            el.addEventListener('close', () => el.dispatchEvent(new Event('hidden.bs.modal')));
            // Escape: bei festen Dialogen (Sitzungswarnung) nicht schließen
            el.addEventListener('cancel', e => { if (el.hasAttribute('data-m-fest')) e.preventDefault(); });
            instanzen.set(el, i);
        }
        return i;
    }

    if (!window.bootstrap) {
        window.bootstrap = { Modal: { getOrCreateInstance: instanz, getInstance: el => instanzen.get(el) || null } };
    }

    document.addEventListener('click', e => {
        const zu = e.target.closest('[data-m-zu], [data-bs-dismiss="modal"]');
        if (zu) {
            const d = zu.closest('dialog');
            if (d) { e.preventDefault(); d.close(); }
            return;
        }
        const auf = e.target.closest('[data-m-blatt]');
        if (auf) {
            const d = document.querySelector(auf.getAttribute('data-m-blatt'));
            if (d && d.showModal) { e.preventDefault(); instanz(d).show(); }
            return;
        }
        // Tipp auf den Hintergrund (das <dialog>-Element selbst, außerhalb seines Rahmens)
        if (e.target instanceof HTMLDialogElement && e.target.open && !e.target.hasAttribute('data-m-fest')) {
            const r = e.target.getBoundingClientRect();
            const innen = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
            if (!innen) e.target.close();
        }
    });

    // ---------- 3. Großer Titel → kleiner Titel in der Kopfzeile ----------
    const kopf = document.querySelector('[data-m-kopf]');
    const titel = document.querySelector('[data-m-titel]');
    if (kopf && titel && 'IntersectionObserver' in window) {
        const hoehe = kopf.getBoundingClientRect().height;
        new IntersectionObserver(eintraege => {
            const sichtbar = eintraege[0].isIntersecting;
            kopf.classList.toggle('m-kopf--titel', !sichtbar);
        }, { rootMargin: `-${Math.round(hoehe)}px 0px 0px 0px`, threshold: 0 }).observe(titel);
    } else if (kopf) {
        // ohne großen Titel: Trennlinie, sobald gescrollt wird
        const linie = () => kopf.classList.toggle('m-kopf--linie', window.scrollY > 4);
        window.addEventListener('scroll', linie, { passive: true });
        linie();
    }

    // ---------- 4. Toast ----------
    window.mToast = function (text) {
        let ort = document.querySelector('.m-toasts');
        if (!ort) {
            ort = document.createElement('div');
            ort.className = 'm-toasts';
            ort.setAttribute('role', 'status');
            ort.setAttribute('aria-live', 'polite');
            document.body.appendChild(ort);
        }
        const t = document.createElement('div');
        t.className = 'm-toast';
        t.textContent = text;
        ort.appendChild(t);
        setTimeout(() => { t.classList.add('m-toast--weg'); setTimeout(() => t.remove(), 400); }, 3200);
    };

    // Meldungen aus dem Server (TempData), die als Toast erscheinen sollen: <template data-m-toast>Text</template>
    document.querySelectorAll('template[data-m-toast]').forEach(t => window.mToast(t.content.textContent.trim()));

    // ---------- 5. Rückfrage vor folgenreichen Formularen (z. B. Termin stornieren): form[data-confirm] ----------
    document.addEventListener('submit', e => {
        const form = e.target.closest('form[data-confirm]');
        if (form && !window.confirm(form.dataset.confirm)) {
            e.preventDefault();
        }
    });

    // ---------- 6. Segment-Filter: [data-m-filter="name"] zeigt die Gruppe [data-gruppe] zum gewählten Wert ----------
    // Karten anderer Gruppen (.appointment-container) bekommen d-none – wie in der bisherigen Oberfläche, die
    // Browser-Tests zählen daran.
    document.querySelectorAll('[data-m-filter]').forEach(segment => {
        const name = segment.getAttribute('data-m-filter');
        segment.querySelectorAll(`input[name="${name}"]`).forEach(radio => {
            radio.addEventListener('change', () => {
                document.querySelectorAll('[data-gruppe]').forEach(gruppe => {
                    const sichtbar = gruppe.dataset.gruppe === radio.value;
                    gruppe.hidden = !sichtbar;
                    gruppe.querySelectorAll('.appointment-container').forEach(k => k.classList.toggle('d-none', !sichtbar));
                });
            });
        });
    });
})();
