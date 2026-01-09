const CACHE_NAME = 'bp-v6'; 
const STATIC_ASSETS = [
    '/',
    '/Antraege/Index',      
    '/Maengel/Create',
    '/css/site.css',
    '/js/site.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/icons/portal-128.png',
    '/icons/portal-512.png',
    '/offline.html'
];

self.addEventListener('install', e => {
    e.waitUntil(
        caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS))
    );
});

self.addEventListener('activate', e => {
    e.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});

self.addEventListener('fetch', e => {
    const req = e.request;
    const url = new URL(req.url);

    if (url.protocol !== 'http:' && url.protocol !== 'https:') return;
    if (req.method !== 'GET') return;

    const bypassPaths = ['/Account/', '/signin-oidc', '/connect/', '/api/'];
    if (bypassPaths.some(p => url.pathname.includes(p))) {
        e.respondWith(fetch(req));
        return;
    }

    if (req.mode === 'navigate' || req.headers.get('accept')?.includes('text/html')) {
        e.respondWith(
            fetch(req)
                .then(res => {
                    const copy = res.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
                    return res;
                })
                .catch(() => caches.match(req) || caches.match('/offline.html'))
        );
        return;
    }

    const isStatic = /\.(?:css|js|png|jpg|jpeg|svg|webp|woff2?)$/i.test(url.pathname);
    if (isStatic) {
        e.respondWith(
            caches.match(req).then(cachedRes => {
                const fetchPromise = fetch(req).then(networkRes => {
                    if (networkRes.ok && networkRes.type !== 'opaque') {
                        const copy = networkRes.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(req, copy));
                    }
                    return networkRes;
                });
                return cachedRes || fetchPromise;
            })
        );
        return;
    }

    e.respondWith(caches.match(req).then(res => res || fetch(req)));
});