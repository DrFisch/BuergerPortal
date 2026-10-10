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
 * Ein Klick auf ein Element mit data-pwa-install installiert die App: Bietet der Browser seinen eigenen Dialog an
 * (beforeinstallprompt – nur Chromium, und nur wenn er die Kriterien erfüllt sieht), erscheint dieser. Sonst öffnet sich
 * der Dialog „App installieren“ mit der Anleitung für das erkannte Gerät (Views/Shared/_AppInstallieren.cshtml) – so
 * gibt es auch auf dem iPhone, in Firefox oder nach einem abgelehnten Browser-Dialog immer einen Weg.
 * In der installierten App (display-mode: standalone) sind alle Einträge ausgeblendet.
 */
const SPAETER_KEY = 'bpsim.pwa.hinweisSpaeter';   // „Später“ beim Hinweis auf der Startseite (Zeitpunkt)
const SPAETER_TAGE = 30;

function isStandalone() {
    return window.matchMedia && window.matchMedia('(display-mode: standalone)').matches
        || window.navigator.standalone === true;
}

/** Gerät/Browser für die passende Anleitung: ios, android, android-firefox, firefox, safari-mac, desktop. */
function plattform() {
    const ua = navigator.userAgent;
    // iPadOS meldet sich als Mac – erkennbar an der Touch-Unterstützung
    if (/iPhone|iPad|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1)) return 'ios';
    if (/Android/.test(ua)) return /Firefox\//.test(ua) ? 'android-firefox' : 'android';
    if (/Firefox\//.test(ua)) return 'firefox';
    if (/Safari\//.test(ua) && !/Chrome|Chromium|Edg\//.test(ua)) return 'safari-mac';
    return 'desktop';
}

function hinweisZurueckgestellt() {
    try {
        const zeit = Number(localStorage.getItem(SPAETER_KEY));
        return zeit > 0 && Date.now() - zeit < SPAETER_TAGE * 24 * 60 * 60 * 1000;
    } catch {
        return false;   // ohne Speicher (privates Fenster): Hinweis zeigen
    }
}

function updateInstallUi() {
    const standalone = isStandalone();
    document.documentElement.classList.toggle('pwa-standalone', standalone);
    document.querySelectorAll('[data-pwa-install-item], [data-pwa-install]').forEach(el => { el.hidden = standalone; });

    // Karte auf der Startseite: immer, solange die App nicht installiert ist
    const card = document.getElementById('pwaInstallCard');
    if (card) card.style.display = standalone ? 'none' : 'block';

    // Hinweis oben auf der Startseite: auf dem Handy oder wenn der Browser das Installieren anbietet – bis „Später“
    const inlineAlert = document.getElementById('pwaInlineInstallAlert');
    if (inlineAlert) {
        const p = plattform();
        const sinnvoll = deferredPrompt || p === 'ios' || p.startsWith('android');
        inlineAlert.classList.toggle('d-none', standalone || !sinnvoll || hinweisZurueckgestellt());
    }
}

function zeigeAnleitung() {
    const modalEl = document.getElementById('appInstallModal');
    if (!modalEl || !window.bootstrap) return;
    const p = plattform();
    modalEl.querySelectorAll('[data-pwa-plattform]').forEach(block => { block.hidden = block.dataset.pwaPlattform !== p; });
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

async function installApp() {
    if (deferredPrompt) {
        try {
            deferredPrompt.prompt();
            await deferredPrompt.userChoice;
            deferredPrompt = null;   // nur einmal verwendbar
            updateInstallUi();
            return;
        } catch (err) {
            console.error('installApp error', err);
        }
    }
    zeigeAnleitung();
}

window.installApp = installApp;

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();   // eigener Knopf statt der Leiste des Browsers
    deferredPrompt = e;
    updateInstallUi();
});

window.addEventListener('appinstalled', () => {
    deferredPrompt = null;
    updateInstallUi();
});

// Alle Knöpfe „App installieren“ (Attribut data-pwa-install) und „Später“ – ohne onclick-Attribut (Content-Security-Policy)
document.addEventListener('click', (e) => {
    if (e.target.closest('[data-pwa-install]')) {
        e.preventDefault();
        installApp();
    } else if (e.target.closest('[data-pwa-spaeter]')) {
        try { localStorage.setItem(SPAETER_KEY, String(Date.now())); } catch { /* ohne Speicher: nur jetzt ausblenden */ }
        document.getElementById('pwaInlineInstallAlert')?.classList.add('d-none');
    }
});

document.addEventListener('DOMContentLoaded', () => {
    updateInstallUi();
    window.matchMedia?.('(display-mode: standalone)').addEventListener?.('change', updateInstallUi);

    window.addEventListener('online', () => document.body.classList.remove('is-offline'));
    window.addEventListener('offline', () => document.body.classList.add('is-offline'));
});
