let deferredPrompt;
let newWorker;


if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').then(reg => {
        reg.addEventListener('updatefound', () => {
            newWorker = reg.installing;
            newWorker.addEventListener('statechange', () => {
                if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                    showUpdateNotification();
                }
            });
        });
    });

    let refreshing;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (refreshing) return;
        window.location.reload();
        refreshing = true;
    });
}

function showUpdateNotification() {

    const updateNow = confirm("Eine neue Version der App ist verfügbar. Möchtest du jetzt aktualisieren?");
    if (updateNow && newWorker) {
        newWorker.postMessage({ type: 'SKIP_WAITING' });
    }
}

function showAppInstallMenuItem() {
    const li = document.getElementById('appInstallMenuItem');
    if (li) {
        li.classList.remove('d-none');
    }
    const btn = document.getElementById('btnInstall');
    if (btn) btn.classList.remove('d-none');
}

function hideAppInstallMenuItem() {
    const li = document.getElementById('appInstallMenuItem');
    if (li) {
        li.classList.add('d-none');
    }
    const btn = document.getElementById('btnInstall');
    if (btn) btn.classList.add('d-none');
}

function isStandalone() {
    return window.matchMedia && window.matchMedia('(display-mode: standalone)').matches
        || window.navigator.standalone === true;
}

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;

    if (!isStandalone()) {
        showAppInstallMenuItem();
    }
});

async function installApp() {
    if (!deferredPrompt) return;
    try {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        console.log(`Installations-Ergebnis: ${outcome}`);
        deferredPrompt = null;
        hideAppInstallMenuItem();
    } catch (err) {
        console.error('installApp error', err);
    }
}

window.installApp = installApp;


document.addEventListener('DOMContentLoaded', () => {
    if (isStandalone()) {
        hideAppInstallMenuItem();
    }

    if (deferredPrompt && !isStandalone()) {
        showAppInstallMenuItem();
    }

    window.addEventListener('online', () => document.body.classList.remove('is-offline'));
    window.addEventListener('offline', () => document.body.classList.add('is-offline'));
});

window.addEventListener('appinstalled', () => {
    hideAppInstallMenuItem();
    deferredPrompt = null;
});