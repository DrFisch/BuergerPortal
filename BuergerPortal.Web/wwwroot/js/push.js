// Benachrichtigungen aufs Handy (Web Push) ein- und ausschalten – Einstellungen, Reiter „Benachrichtigungen“.
// Ablauf: öffentlichen VAPID-Schlüssel holen (/push/key) → Erlaubnis des Browsers → Abo beim Push-Dienst des Browsers
// (pushManager.subscribe) → Abo ans Portal (/push/subscriptions, mit Antiforgery-Token) → das Portal reicht es an die
// API weiter. Das Abo gilt für dieses Gerät und diesen Browser, nicht für das ganze Konto.
(function () {
    'use strict';

    const box = document.querySelector('[data-push]');
    if (!box) return;

    const el = name => box.querySelector(`[data-push-${name}]`);
    const text = key => box.dataset[key] || '';
    let publicKey = null;

    function antiforgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function send(url, method, body) {
        return fetch(url, {
            method,
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiforgeryToken() },
            body: body ? JSON.stringify(body) : undefined
        });
    }

    async function errorText(res) {
        try {
            const problem = await res.json();
            return problem.detail || problem.title || text('msgError');
        } catch {
            return text('msgError');
        }
    }

    function base64UrlToBytes(value) {
        const padded = value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - value.length % 4) % 4);
        return Uint8Array.from(atob(padded), c => c.charCodeAt(0));
    }

    // Abo noch mit dem aktuellen Schlüssel des Servers? (nach einem Schlüsselwechsel lehnt der Push-Dienst ab)
    function sameKey(subscription) {
        const current = subscription.options && subscription.options.applicationServerKey;
        if (!current || !publicKey) return true;
        const a = new Uint8Array(current);
        const b = base64UrlToBytes(publicKey);
        return a.length === b.length && a.every((v, i) => v === b[i]);
    }

    function show(status, { on = false, off = false, test = false, hint = null } = {}) {
        el('status').textContent = status;
        el('status').className = 'badge ' + (off ? 'text-bg-success' : 'text-bg-secondary');
        el('an').hidden = !on;
        el('aus').hidden = !off;
        el('test').hidden = !test;
        box.querySelectorAll('[data-push-hinweis]').forEach(h => { h.hidden = h.dataset.pushHinweis !== hint; });
    }

    function message(value, ok) {
        const m = el('meldung');
        m.textContent = value || '';
        m.className = 'small mt-2 ' + (ok ? 'text-success' : 'text-danger');
    }

    async function refresh() {
        const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
        if (!supported) {
            // iPhone/iPad: Web Push nur in der installierten App (ab iOS 16.4), nicht im Safari-Tab
            const ios = typeof plattform === 'function' && plattform() === 'ios'
                && !(typeof isStandalone === 'function' && isStandalone());
            show(text('statusUnsupported'), { hint: ios ? 'ios' : 'unsupported' });
            return;
        }
        const res = await fetch('/push/key', { credentials: 'same-origin' }).catch(() => null);
        if (!res || !res.ok) {
            show(text('statusNotConfigured'), { hint: 'not-configured' });
            return;
        }
        publicKey = (await res.json()).publicKey;
        if (Notification.permission === 'denied') {
            show(text('statusDenied'), { hint: 'denied' });
            return;
        }
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        if (subscription && sameKey(subscription)) {
            show(text('statusOn'), { off: true, test: true });
        } else {
            show(text('statusOff'), { on: true });
        }
    }

    async function turnOn() {
        message('');
        if (await Notification.requestPermission() !== 'granted') {
            await refresh();
            return;
        }
        const registration = await navigator.serviceWorker.ready;
        let subscription = await registration.pushManager.getSubscription();
        if (subscription && !sameKey(subscription)) {
            await subscription.unsubscribe();
            subscription = null;
        }
        subscription = subscription || await registration.pushManager.subscribe({
            userVisibleOnly: true,   // jede Nachricht wird sichtbar angezeigt (Pflicht in Chrome und Safari)
            applicationServerKey: base64UrlToBytes(publicKey)
        });
        const res = await send('/push/subscriptions', 'POST', subscription.toJSON());
        if (res.ok) {
            message(text('msgOn'), true);
        } else {
            await subscription.unsubscribe();
            message(await errorText(res), false);
        }
        await refresh();
    }

    async function turnOff() {
        message('');
        const registration = await navigator.serviceWorker.ready;
        const subscription = await registration.pushManager.getSubscription();
        if (subscription) {
            await send('/push/subscriptions?endpoint=' + encodeURIComponent(subscription.endpoint), 'DELETE').catch(() => null);
            await subscription.unsubscribe();
        }
        message(text('msgOff'), true);
        await refresh();
    }

    async function sendTest() {
        message('');
        const res = await send('/push/test', 'POST');
        if (!res.ok) {
            message(await errorText(res), false);
            return;
        }
        const { sent } = await res.json();
        message(sent > 0 ? text('msgTestSent') : text('msgTestNone'), sent > 0);
    }

    function guarded(action) {
        return async () => {
            box.querySelectorAll('button').forEach(b => { b.disabled = true; });
            try {
                await action();
            } catch (e) {
                message(text('msgError'), false);
                console.warn('Push:', e);
            } finally {
                box.querySelectorAll('button').forEach(b => { b.disabled = false; });
            }
        };
    }

    el('an').addEventListener('click', guarded(turnOn));
    el('aus').addEventListener('click', guarded(turnOff));
    el('test').addEventListener('click', guarded(sendTest));
    refresh().catch(e => console.warn('Push:', e));
})();
