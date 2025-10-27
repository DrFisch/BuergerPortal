(function () {
    const btn = document.getElementById('enablePush');
    const status = document.getElementById('pushStatus');
    if (!btn) return;

    const env = document.querySelector('meta[name="app-env"]')?.content ?? 'Production';
    const isProd = env === 'Production';

    function setStatus(msg) { if (status) status.textContent = msg; }

    if (!isProd) {
        btn.addEventListener('click', () => setStatus('Push ist in dieser Umgebung deaktiviert.'));
        return;
    }

    btn.addEventListener('click', async () => {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
                setStatus('Push wird nicht unterstützt.');
                return;
            }
            const perm = await Notification.requestPermission();
            if (perm !== 'granted') { setStatus('Benachrichtigungen nicht erlaubt.'); return; }

            const reg = await navigator.serviceWorker.ready;
            const vapidPublicKey = await fetch('/push/publickey').then(r => r.text());
            const applicationServerKey = urlBase64ToUint8Array(vapidPublicKey);

            const subscription = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey });

            await fetch('/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(subscription)
            });

            setStatus('Push-Benachrichtigungen aktiviert ✅');
        } catch (err) {
            console.error(err);
            setStatus('Fehler beim Aktivieren von Push.');
        }
    });

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const out = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; ++i) out[i] = raw.charCodeAt(i);
        return out;
    }
})();
