let deferredPrompt;
let newWorker;

/**
 * SERVICE WORKER REGISTRIERUNG & UPDATE-LOGIK
 */
if ('serviceWorker' in navigator) {
    // Einzige Registrierung des Service Workers (vorher zusätzlich im Layout); sw.js nie aus dem HTTP-Cache.
    navigator.serviceWorker.register('/sw.js', { updateViaCache: 'none' }).then(reg => {
        reg.addEventListener('updatefound', () => {
            newWorker = reg.installing;
            newWorker.addEventListener('statechange', () => {
                if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                    showUpdateNotification();
                }
            });
        });
    });

    // Neu laden nur nach einem bestätigten Update. Beim ersten Besuch übernimmt der Service Worker die Seite ebenfalls
    // (clients.claim → controllerchange); ein Neuladen in diesem Moment brach z. B. den Klick auf „Mit BundID anmelden“ ab.
    let refreshing;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (refreshing || !updateAccepted) return;
        refreshing = true;
        window.location.reload();
    });
}

let updateAccepted = false;

function showUpdateNotification() {
    const updateNow = confirm("Eine neue Version des BürgerPortals ist verfügbar. Jetzt aktualisieren?");
    if (updateNow && newWorker) {
        updateAccepted = true;
        newWorker.postMessage({ type: 'SKIP_WAITING' });
    }
}

/**
 * PWA INSTALLATIONS-LOGIK & UI-STEUERUNG
 */
function showInstallPrompts() {
    // Falls wir im Standalone-Modus sind, nichts anzeigen
    if (isStandalone()) return;

    // 1. Die Feature-Card auf dem Home-Bildschirm (falls vorhanden)
    const card = document.getElementById('pwaInstallCard');
    if (card) card.style.display = 'block';

    // 2. Der dezente Alert über dem Login-Hinweis
    const inlineAlert = document.getElementById('pwaInlineInstallAlert');
    if (inlineAlert) inlineAlert.classList.remove('d-none');

    // 3. Optional: Einmaliger Toast oder Browser-Alert (nur bei erstem Laden)
    if (!sessionStorage.getItem('pwa_alert_shown')) {
        // Hier ein dezenter Toast oder einfacher Alert
        console.log("Hinweis: Installieren Sie diese App für eine bessere Erfahrung.");
        sessionStorage.setItem('pwa_alert_shown', 'true');
    }
}

function hideInstallPrompts() {
    const card = document.getElementById('pwaInstallCard');
    if (card) card.style.display = 'none';

    const inlineAlert = document.getElementById('pwaInlineInstallAlert');
    if (inlineAlert) inlineAlert.classList.add('d-none');
}

function isStandalone() {
    return window.matchMedia && window.matchMedia('(display-mode: standalone)').matches
        || window.navigator.standalone === true;
}

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
    showInstallPrompts();
});

async function installApp() {
    if (!deferredPrompt) return;
    try {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        console.log(`Installations-Ergebnis: ${outcome}`);
        deferredPrompt = null;
        hideInstallPrompts();
    } catch (err) {
        console.error('installApp error', err);
    }
}

window.installApp = installApp;

document.addEventListener('DOMContentLoaded', () => {
    if (isStandalone()) {
        hideInstallPrompts();
    }

    // Falls das Event schon gefeuert wurde
    if (deferredPrompt) showInstallPrompts();

    window.addEventListener('online', () => document.body.classList.remove('is-offline'));
    window.addEventListener('offline', () => document.body.classList.add('is-offline'));
});

window.addEventListener('appinstalled', () => {
    hideInstallPrompts();
    deferredPrompt = null;
});