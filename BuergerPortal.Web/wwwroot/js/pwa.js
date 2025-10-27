// Theme Toggle
document.getElementById("themeToggle")?.addEventListener("click", () => {
    document.body.classList.toggle("dark");
    localStorage.setItem("theme", document.body.classList.contains("dark") ? "dark" : "light");
});
if (localStorage.getItem("theme") === "dark") document.body.classList.add("dark");

// Service Worker registrieren
(async () => {
    if ("serviceWorker" in navigator) {
        try {
            await navigator.serviceWorker.register("/service-worker.js");
        } catch (e) {
            console.warn("SW registration failed", e);
        }
    }
})();

// Push aktivieren
document.getElementById("enablePush")?.addEventListener("click", async () => {
    const status = document.getElementById("pushStatus");
    try {
        if (!("serviceWorker" in navigator) || !("PushManager" in window)) {
            status.textContent = "Push wird nicht unterstützt.";
            return;
        }

        const perm = await Notification.requestPermission();
        if (perm !== "granted") {
            status.textContent = "Benachrichtigungen nicht erlaubt.";
            return;
        }

        const reg = await navigator.serviceWorker.ready;
        const vapidPublicKey = await fetch("/push/publickey").then(r => r.text());
        const keyUint8 = urlBase64ToUint8Array(vapidPublicKey);

        const subscription = await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: keyUint8
        });

        await fetch("/push/subscribe", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(subscription)
        });

        status.textContent = "Push-Benachrichtigungen aktiviert ✅";
    } catch (err) {
        console.error(err);
        status.textContent = "Fehler beim Aktivieren von Push.";
    }
});

// Helper: Base64 → Uint8Array
function urlBase64ToUint8Array(base64String) {
    const padding = "=".repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
    const raw = atob(base64);
    const out = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; ++i) out[i] = raw.charCodeAt(i);
    return out;
}
