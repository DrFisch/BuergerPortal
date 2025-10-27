(async () => {
    if (!('serviceWorker' in navigator)) return;

    const env = document.querySelector('meta[name="app-env"]')?.content ?? 'Production';
    const isProd = env === 'Production';

    if (!isProd) {
        // In Dev/Local: sicherstellen, dass kein SW aktiv bleibt
        const regs = await navigator.serviceWorker.getRegistrations();
        for (const reg of regs) await reg.unregister();
        if ('caches' in window) {
            const keys = await caches.keys();
            await Promise.all(keys.map(k => caches.delete(k)));
        }
        return;
    }

    try {
        await navigator.serviceWorker.register(`/service-worker.js?env=${encodeURIComponent(env)}`, { scope: '/' });
    } catch (e) {
        console.warn('SW registration failed', e);
    }
})();
