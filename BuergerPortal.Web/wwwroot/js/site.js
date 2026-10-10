function updateOnlineStatus() {
    const offline = !navigator.onLine;
    document.body.classList.toggle('app-is-offline', offline);
    // Hinweisleiste unten (Views/Shared/_AppHinweise.cshtml): Ohne Netz zeigt die App nur die Offline-Seite.
    const banner = document.getElementById('offlineBanner');
    if (banner) banner.hidden = !offline;
}

window.addEventListener('online', updateOnlineStatus);
window.addEventListener('offline', updateOnlineStatus);
updateOnlineStatus();