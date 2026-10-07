// Service Worker der Web-App (PWA).
// Seiten (HTML) kommen immer frisch vom Server und werden NICHT zwischengespeichert: Sie enthalten persönliche Daten
// (z. B. „Meine Daten“ mit Name, Anschrift, bPK2), die nach dem Abmelden nicht im Browser bleiben dürfen. Ohne Netz
// erscheint die Offline-Seite. Zwischengespeichert werden nur statische Dateien (CSS, JS, Bilder, Schriften).
// Neue Cache-Version: Beim Aktivieren werden ältere Caches gelöscht – auch bp-v6, das noch HTML-Seiten enthielt.
const CACHE_NAME = 'bp-static-v7';
const OFFLINE_URL = '/offline.html';
const PRECACHE = [
    OFFLINE_URL,
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/icons/portal-128.png',
    '/icons/portal-512.png'
];

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE_NAME).then(cache => cache.addAll(PRECACHE)));
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys()
            .then(keys => Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('message', event => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

self.addEventListener('fetch', e => {
    const req = e.request;
    const url = new URL(req.url);

    // Nur eigene GET-Anfragen; Anmeldung, API und Abfragen der Seiten (z. B. /Termine/Verfuegbarkeit) laufen ungestört.
    if (req.method !== 'GET' || url.origin !== self.location.origin) {
        return;
    }

    // Seiten: immer übers Netz, ohne Zwischenspeicher; nur ohne Netz die Offline-Seite.
    if (req.mode === 'navigate') {
        e.respondWith(fetch(req).catch(async () => (await caches.match(OFFLINE_URL)) || Response.error()));
        return;
    }

    // Statische Dateien: aus dem Cache, im Hintergrund aktualisieren (stale-while-revalidate).
    if (/\.(?:css|js|png|jpg|jpeg|svg|webp|ico|woff2?)$/i.test(url.pathname)) {
        e.respondWith(
            caches.open(CACHE_NAME).then(async cache => {
                const cached = await cache.match(req);
                const network = fetch(req).then(res => {
                    if (res.ok) {
                        cache.put(req, res.clone());
                    }
                    return res;
                }).catch(() => cached || Response.error());
                return cached || network;
            })
        );
    }
    // Alles andere (JSON-Abfragen usw.): normal übers Netz, ohne Eingriff.
});
