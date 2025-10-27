/* Basic SW: Cache Shell + Fetch passthrough */
const CACHE = "cp-cache-v1";
const ASSETS = ["/", "/css/site.css"];

self.addEventListener("install", (e) => {
    e.waitUntil(caches.open(CACHE).then(c => c.addAll(ASSETS)));
});

self.addEventListener("activate", (e) => { self.clients.claim(); });

self.addEventListener("fetch", (e) => {
    e.respondWith(
        caches.match(e.request).then(r => r || fetch(e.request))
    );
});
