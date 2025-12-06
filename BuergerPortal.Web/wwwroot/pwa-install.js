let deferredPrompt;

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

window.addEventListener('beforeinstallprompt', (e) => {
    // Chrome: prevent automatic prompt and store event for later
    e.preventDefault();
    deferredPrompt = e;
    showAppInstallMenuItem();
});

async function installApp() {
    if (!deferredPrompt) return;
    try {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        deferredPrompt = null;
        // hide irrespective of user choice; once used we don't show again
        hideAppInstallMenuItem();
    } catch (err) {
        console.error('installApp error', err);
    }
}

window.installApp = installApp;

// If the app is installed (or running in standalone), hide install item
function isStandalone() {
    return window.matchMedia && window.matchMedia('(display-mode: standalone)').matches
        || window.navigator.standalone === true;
}

document.addEventListener('DOMContentLoaded', () => {
    if (isStandalone()) {
        hideAppInstallMenuItem();
    }
    // Also, if beforeinstallprompt already fired earlier, show it
    // (rare in SPA-less setups, but safe)
    if (deferredPrompt) showAppInstallMenuItem();
});

window.addEventListener('appinstalled', () => {
    hideAppInstallMenuItem();
});