function updateOnlineStatus() {
    const status = navigator.onLine ? "online" : "offline";
    if (status === "offline") {
        // Zeige einen dezenten Hinweis im Header an
        document.body.classList.add('app-is-offline');
        console.log("App ist im Offline-Modus");
    } else {
        document.body.classList.remove('app-is-offline');
    }
}

window.addEventListener('online', updateOnlineStatus);
window.addEventListener('offline', updateOnlineStatus);
updateOnlineStatus();