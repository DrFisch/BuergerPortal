// Foto zur Mängelmeldung (Views/Maengel/Create.cshtml): Kamera oder Bild auswählen, im Browser verkleinern und erst dann
// hochladen. Beim Neuzeichnen auf ein Canvas fallen die Metadaten des Fotos weg (EXIF, auch der GPS-Ort der Kamera) –
// der Ort kommt nur aus der Karte, die die Person selbst setzt. Ein über „Teilen“ empfangenes Foto (share_target, siehe
// sw.js) liegt kurz im Cache „bp-geteilt“ und wird hier übernommen.
(function () {
    'use strict';
    const MAX_KANTE = 1600;
    const upload = document.getElementById('fotoUpload');
    if (!upload) {
        return;
    }
    const auswahl = document.querySelectorAll('[data-foto-quelle]');
    const vorschau = document.getElementById('fotoVorschau');
    const vorschauBox = document.getElementById('fotoVorschauBox');
    const status = document.getElementById('fotoStatus');
    const entfernen = document.getElementById('fotoEntfernen');

    async function verkleinern(datei) {
        let bild;
        try {
            // Dreht nach der Kamera-Ausrichtung, bevor die Metadaten wegfallen
            bild = await createImageBitmap(datei, { imageOrientation: 'from-image' });
        } catch (e) {
            bild = await new Promise(function (resolve, reject) {
                const img = new Image();
                img.onload = function () { resolve(img); };
                img.onerror = reject;
                img.src = URL.createObjectURL(datei);
            });
        }
        const faktor = Math.min(1, MAX_KANTE / Math.max(bild.width, bild.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(bild.width * faktor);
        canvas.height = Math.round(bild.height * faktor);
        canvas.getContext('2d').drawImage(bild, 0, 0, canvas.width, canvas.height);
        const blob = await new Promise(function (resolve) { canvas.toBlob(resolve, 'image/jpeg', 0.85); });
        return new File([blob], 'foto.jpg', { type: 'image/jpeg' });
    }

    // Meldungen übersetzt aus data-t-* am Statusfeld (Views/Maengel/Create.cshtml, Plan A9); sonst Deutsch
    function text(name, standard) {
        return status.dataset['t' + name.charAt(0).toUpperCase() + name.slice(1)] || standard;
    }

    async function uebernehmen(datei) {
        if (!datei || !datei.type.startsWith('image/')) {
            status.textContent = text('auswahl', 'Bitte ein Foto auswählen.');
            return;
        }
        status.textContent = text('verkleinern', 'Foto wird verkleinert …');
        try {
            const klein = await verkleinern(datei);
            const transfer = new DataTransfer();
            transfer.items.add(klein);
            upload.files = transfer.files;
            vorschau.src = URL.createObjectURL(klein);
            vorschauBox.hidden = false;
            status.textContent = text('angehaengt', 'Foto angehängt ({0} KB, ohne Ortsangaben aus der Kamera).')
                .replace('{0}', String(Math.round(klein.size / 1024)));
        } catch (e) {
            status.textContent = text('fehler', 'Das Foto konnte nicht verarbeitet werden. Bitte ein anderes wählen.');
        }
    }

    auswahl.forEach(function (input) {
        input.addEventListener('change', function () {
            uebernehmen(input.files && input.files[0]);
            input.value = '';
        });
    });

    entfernen.addEventListener('click', function () {
        upload.value = '';
        vorschauBox.hidden = true;
        vorschau.removeAttribute('src');
        status.textContent = '';
    });

    // Über „Teilen“ empfangenes Foto übernehmen und sofort aus dem Cache löschen
    if (new URLSearchParams(location.search).has('geteilt') && 'caches' in window) {
        caches.open('bp-geteilt').then(async function (cache) {
            const antwort = await cache.match('/geteilt/foto');
            if (antwort) {
                const blob = await antwort.blob();
                await cache.delete('/geteilt/foto');
                await uebernehmen(new File([blob], 'geteilt', { type: blob.type || 'image/jpeg' }));
            }
        }).catch(function () { /* ohne Cache bleibt das Formular leer */ });
    }
})();
