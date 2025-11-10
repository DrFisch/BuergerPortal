// sw.js
const CACHE = 'bp-v4';                 // Version hochzählen bei Änderungen
const STATIC_ASSETS = [
    '/css/site.css',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/js/site.js',
    '/icons/portal-128.png',
    '/icons/portal-512.png',
    '/offline.html'
];

self.addEventListener('install', e => {
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(STATIC_ASSETS)));
    self.skipWaiting();
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
    );
    self.clients.claim();
});

self.addEventListener('fetch', (e) => {
    const req = e.request;
    const url = new URL(req.url);

    // Nicht-GET: immer Netzwerk
    if (req.method !== 'GET') { e.respondWith(fetch(req)); return; }

    // Auth/OIDC/Logout niemals cachen
    const noCachePaths = ['/Account/Login', '/Account/Logout', '/signin-oidc', '/signout-callback-oidc', '/connect/', '/Identity/Account/'];
    if (url.pathname.startsWith('/api/') || noCachePaths.some(p => url.pathname.startsWith(p))) {
        e.respondWith(fetch(req, { cache: 'no-store' }));
        return;
    }

    // HTML/Navigationsanfragen → network-first (wichtig!)
    const isHtml = req.mode === 'navigate' || req.headers.get('accept')?.includes('text/html');
    if (isHtml) {
        e.respondWith(fetch(req, { cache: 'no-store' }).catch(() => caches.match('/offline.html')));
        return;
    }

    // Statische Assets → cache-first
    const STATIC_EXT = /\.(?:css|js|png|jpg|jpeg|svg|webp|ico|woff2?)$/i;
    if (STATIC_EXT.test(url.pathname)) {
        e.respondWith(
            caches.match(req).then(hit => hit || fetch(req).then(res => {
                const copy = res.clone(); caches.open('bp-v4').then(c => c.put(req, copy)); return res;
            }))
        );
        return;
    }

    // Default
    e.respondWith(fetch(req).catch(() => caches.match(req) || caches.match('/offline.html')));
});
