// Antragsseiten (2026-10-18): kleine Helfer, als Datei (CSP script-src 'self'). Ohne Skript funktionieren alle Formulare
// ebenso – die Helfer machen sie nur bequemer.
//  - [data-ap-gebuehr]: Gebühr live nach dem Express-Schalter (data-basis, data-express in Euro)
//  - [data-ap-plus] / [data-ap-minus]: Mengen-Stepper für ein Zahlenfeld (data-ziel = id)
//  - [data-ap-tag]: Tages-Chip setzt das Datumsfeld (data-ziel = id) und markiert sich
//  - form[data-ap-bestaetigen]: Rückfrage vor dem Absenden (z. B. Entwurf verwerfen)
(function () {
    'use strict';

    var sprache = document.documentElement.lang || 'de';
    var euro = new Intl.NumberFormat(sprache, { style: 'currency', currency: 'EUR' });

    function gebuehr() {
        document.querySelectorAll('[data-ap-gebuehr]').forEach(function (el) {
            var schalter = document.getElementById(el.getAttribute('data-schalter'));
            var betrag = parseFloat(el.getAttribute('data-basis')) +
                (schalter && schalter.checked ? parseFloat(el.getAttribute('data-express')) : 0);
            el.textContent = euro.format(betrag);
        });
    }

    function stepper(knopf, schritt) {
        var feld = document.getElementById(knopf.getAttribute('data-ziel'));
        if (!feld) return;
        var min = feld.min === '' ? 0 : parseInt(feld.min, 10);
        var max = feld.max === '' ? 99 : parseInt(feld.max, 10);
        var wert = (parseInt(feld.value, 10) || 0) + schritt;
        feld.value = Math.max(min, Math.min(max, wert));
        feld.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function tageMarkieren(feld) {
        document.querySelectorAll('[data-ap-tag][data-ziel="' + feld.id + '"]').forEach(function (chip) {
            chip.setAttribute('aria-pressed', chip.getAttribute('data-ap-tag') === feld.value ? 'true' : 'false');
        });
    }

    document.addEventListener('click', function (e) {
        var plus = e.target.closest('[data-ap-plus]');
        var minus = e.target.closest('[data-ap-minus]');
        var tag = e.target.closest('[data-ap-tag]');
        if (plus) stepper(plus, 1);
        if (minus) stepper(minus, -1);
        if (tag) {
            var feld = document.getElementById(tag.getAttribute('data-ziel'));
            if (feld) {
                feld.value = tag.getAttribute('data-ap-tag');
                tageMarkieren(feld);
            }
        }
    });

    document.addEventListener('change', function (e) {
        if (e.target.matches('[data-ap-express]')) gebuehr();
        if (e.target.type === 'date') tageMarkieren(e.target);
    });

    document.addEventListener('submit', function (e) {
        var frage = e.target.getAttribute('data-ap-bestaetigen');
        if (frage && !window.confirm(frage)) e.preventDefault();
    });

    document.addEventListener('DOMContentLoaded', function () {
        gebuehr();
        document.querySelectorAll('input[type=date]').forEach(tageMarkieren);
    });
})();
