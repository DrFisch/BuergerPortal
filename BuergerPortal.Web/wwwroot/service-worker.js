const url = new URL(self.location);
const ENV = url.searchParams.get('env') || 'Production';
const CACHE_ENABLED = (ENV === 'Production');

const CACHE = 'bp-static-v6';
const PRECACHE_ASSETS = [
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/bootstrap-icons/font/bootstrap-icons.min.css',
    '/css/site.css'
];

self.addEventListener('install', (e) => {
    if (CACHE_ENABLED) {
        e.waitUntil(caches.open(CACHE).then(c => c.addAll(PRECACHE_ASSETS)));
    }
    self.skipWaiting();
});

self.addEventListener('activate', (e) => {
    if (CACHE_ENABLED) {
        e.waitUntil(
            caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
        );
    }
    self.clients.claim();
});

self.addEventListener('fetch', (e) => {
    const req = e.request;

    // HTML immer Netz (kein altes / aus Cache)
    if (req.mode === 'navigate' || (req.destination === '' && req.headers.get('accept')?.includes('text/html'))) {
        if (!CACHE_ENABLED) return; // in Dev/Local gar nichts abfangen
        e.respondWith(fetch(req).catch(() => new Response('Offline', { status: 503 })));
        return;
    }

    // Assets nur in Prod cachen
    const isAsset = ['style', 'script', 'font', 'image'].includes(req.destination);
    if (isAsset && CACHE_ENABLED) {
        e.respondWith(
            caches.match(req).then(hit => hit || fetch(req).then(res => {
                if (res && res.status === 200) caches.open(CACHE).then(c => c.put(req, res.clone()));
                return res;
            }))
        );
    }
});

// (optional) Push-Event – nur relevant, wenn SW in Prod registriert ist
self.addEventListener('push', (event) => {
    try {
        const data = event.data ? event.data.json() : {};
        const title = data.title || 'Bürgerportal';
        const body = data.body || 'Neue Benachrichtigung';
        event.waitUntil(
            self.registration.showNotification(title, {
                body,
                icon: '/icons/icon-192.png',
                badge: '/icons/badge-72.png'
            })
        );
    } catch { /* no-op */ }
});
