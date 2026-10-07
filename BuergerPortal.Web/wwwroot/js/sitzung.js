// Warnung vor dem automatischen Abmelden (Views/Shared/_SitzungsWarnung.cshtml).
// Die Restzeit in Sekunden steht nach jedem Seitenaufruf im Meta-Element bpsim-sitzung. Zwei Minuten vor Ablauf fragt das
// Skript den Server (GET /Auth/Sitzung zählt nicht als Aktivität) – war die Person in einem anderen Tab aktiv, wird nur
// neu geplant; sonst erscheint die Warnung mit Countdown. „Angemeldet bleiben“ verlängert (POST /Auth/Sitzung).
(function () {
    const meta = document.querySelector('meta[name="bpsim-sitzung"]');
    const modalEl = document.getElementById('sitzungModal');
    if (!meta || !modalEl || !window.bootstrap) {
        return;
    }
    const WARNUNG_SEKUNDEN = 120;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const anzeige = modalEl.querySelector('[data-sitzung-rest]');
    const verlaengern = modalEl.querySelector('[data-sitzung-verlaengern]');
    let ende = Date.now() + parseInt(meta.content, 10) * 1000;
    let timer = null;
    let takt = null;

    async function status(method) {
        try {
            const res = await fetch('/Auth/Sitzung', {
                method: method,
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch' }
            });
            return res.ok ? await res.json() : null;
        } catch (e) {
            return null; // Netzwerkfehler: lieber erneut versuchen als abmelden
        }
    }

    function planen() {
        clearTimeout(timer);
        timer = setTimeout(pruefen, Math.max(ende - Date.now() - WARNUNG_SEKUNDEN * 1000, 0));
    }

    function abgelaufen() {
        window.location.href = '/?sitzung=abgelaufen';
    }

    function uebernehmen(s) {
        ende = Date.now() + s.restSekunden * 1000;
        modalEl.querySelector('[data-sitzung-normal]').hidden = s.hoechstdauer;
        modalEl.querySelector('[data-sitzung-hoechstdauer]').hidden = !s.hoechstdauer;
        verlaengern.hidden = s.hoechstdauer; // Höchstdauer: Verlängern geht nicht mehr
    }

    async function pruefen() {
        const s = await status('GET');
        if (s === null) {
            timer = setTimeout(pruefen, 15000);
            return;
        }
        if (!s.angemeldet) {
            abgelaufen();
            return;
        }
        uebernehmen(s);
        if (s.restSekunden <= 0) {
            // Die Uhr des Browsers ist etwas voraus: erst weiterleiten, wenn der Server die Sitzung beendet hat –
            // sonst wäre der Seitenaufruf noch angemeldet und würde die Sitzung verlängern.
            timer = setTimeout(pruefen, 1500);
            return;
        }
        if (s.restSekunden > WARNUNG_SEKUNDEN + 5) {
            planen(); // in einem anderen Tab aktiv gewesen
            return;
        }
        zeigen();
    }

    function zeigen() {
        modal.show();
        clearInterval(takt);
        takt = setInterval(aktualisieren, 1000);
        aktualisieren();
    }

    function aktualisieren() {
        const rest = Math.max(0, Math.round((ende - Date.now()) / 1000));
        anzeige.textContent = Math.floor(rest / 60) + ':' + String(rest % 60).padStart(2, '0');
        if (rest <= 0) {
            clearInterval(takt);
            pruefen();
        }
    }

    verlaengern.addEventListener('click', async function () {
        const s = await status('POST');
        if (!s || !s.angemeldet) {
            abgelaufen();
            return;
        }
        uebernehmen(s);
        clearInterval(takt);
        modal.hide();
        planen();
    });

    planen();
})();
