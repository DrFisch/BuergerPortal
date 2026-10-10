// Service Worker der Web-App (PWA).
// Seiten (HTML) kommen immer frisch vom Server und werden NICHT zwischengespeichert: Sie enthalten persönliche Daten
// (z. B. „Meine Daten“ mit Name, Anschrift, bPK2), die nach dem Abmelden nicht im Browser bleiben dürfen. Ohne Netz
// erscheint die Offline-Seite. Zwischengespeichert werden nur statische Dateien (CSS, JS, Bilder, Schriften).
// Neue Cache-Version: Beim Aktivieren werden ältere Caches gelöscht – auch bp-v6, das noch HTML-Seiten enthielt.
// v8: Offline-Seite ohne Inline-Skript (Content-Security-Policy). Seit 6.88 zusätzlich Benachrichtigungen (push, notificationclick).
const CACHE_NAME = 'bp-static-v8';
const OFFLINE_URL = '/offline.html';
const PRECACHE = [
    OFFLINE_URL,
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/icons/portal-128.png',
    '/icons/portal-192.png',
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

// Benachrichtigungen (Web Push): Die API schickt {title, body, url, tag} verschlüsselt über den Push-Dienst des Browsers
// (RFC 8291); der Browser entschlüsselt, hier wird nur angezeigt. Ein Tipp öffnet die Seite (z. B. /Postfach).
self.addEventListener('push', e => {
    let data = {};
    try {
        data = e.data ? e.data.json() : {};
    } catch {
        data = { body: e.data ? e.data.text() : '' };
    }
    // nur eigene Seiten öffnen (Pfad, keine fremde oder protokollrelative Adresse)
    const url = typeof data.url === 'string' && data.url.startsWith('/') && !data.url.startsWith('//') ? data.url : '/';
    e.waitUntil(Promise.all([
        self.registration.showNotification(data.title || 'BürgerPortal', {
            body: data.body || '',
            icon: '/icons/portal-192.png',
            tag: data.tag || 'buergerportal',
            renotify: true,   // gleiche Art (tag) ersetzt die alte Mitteilung, meldet sich aber erneut
            data: { url }
        }),
        // Punkt am App-Symbol (Badging API: Android/Chrome, Windows/macOS, iOS ab 16.4 als installierte App)
        self.navigator.setAppBadge ? self.navigator.setAppBadge().catch(() => { }) : Promise.resolve()
    ]));
});

self.addEventListener('notificationclick', e => {
    e.notification.close();
    const url = new URL((e.notification.data && e.notification.data.url) || '/', self.location.origin).href;
    e.waitUntil((async () => {
        // offenes Fenster der App nutzen, sonst ein neues öffnen
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        const open = windows.find(w => new URL(w.url).origin === self.location.origin);
        if (open) {
            await open.focus();
            return open.navigate(url).catch(() => self.clients.openWindow(url));
        }
        return self.clients.openWindow(url);
    })());
});
