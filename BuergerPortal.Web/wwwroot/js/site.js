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

// Punkt am App-Symbol (Badging API, gesetzt vom Service Worker bei einer Benachrichtigung) auf der Postfach-Seite entfernen
if (document.querySelector('[data-app-badge-clear]') && navigator.clearAppBadge) {
    navigator.clearAppBadge().catch(() => { });
}