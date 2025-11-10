let deferredPrompt;

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
    document.querySelector('#btnInstall')?.classList.remove('d-none');
});

async function installApp() {
    if (!deferredPrompt) return;
    deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    deferredPrompt = null;
    document.querySelector('#btnInstall')?.classList.add('d-none');
}

window.addEventListener('appinstalled', () => {
    document.querySelector('#btnInstall')?.classList.add('d-none');
});

window.installApp = installApp;
