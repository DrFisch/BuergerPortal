namespace BuergerPortal.Api.Push
{
    /// <summary>
    /// Abschnitt "Push": VAPID-Schlüsselpaar (base64url, öffentlich 65 Byte unkomprimiert, privat 32 Byte), Kontakt des
    /// Betreibers für die Push-Dienste und die erlaubten Push-Dienste. Ohne Schlüssel sind Benachrichtigungen aus.
    /// </summary>
    public sealed class PushOptions
    {
        public const string Section = "Push";

        /// <summary>
        /// Push-Dienste der verbreiteten Browser: Chrome/Android/Samsung/Opera (Google FCM), Edge (Microsoft WNS),
        /// Firefox (Mozilla), Safari/iOS (Apple). „*.“ = beliebige Subdomain.
        /// </summary>
        public static readonly string[] DefaultAllowedHosts =
            ["fcm.googleapis.com", "*.notify.windows.com", "updates.push.services.mozilla.com", "*.push.apple.com"];

        public string? VapidPublicKey { get; set; }
        public string? VapidPrivateKey { get; set; }

        /// <summary>Kontakt für die Push-Dienste (RFC 8292: „mailto:…“ oder „https://…“).</summary>
        public string? Subject { get; set; }

        /// <summary>Eigene Liste statt <see cref="DefaultAllowedHosts"/> (leer = Standard).</summary>
        public string[]? AllowedHosts { get; set; }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey) && !string.IsNullOrWhiteSpace(Subject);

        /// <summary>
        /// Nur https zu einem bekannten Push-Dienst. Die Adresse eines Abos liefert der Browser – ohne diese Prüfung
        /// könnte jemand eine interne Adresse eintragen und die API Anfragen ins eigene Netz schicken lassen (SSRF).
        /// </summary>
        public bool IsAllowedEndpoint(Uri endpoint)
        {
            if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort
                || endpoint.HostNameType != UriHostNameType.Dns)
            {
                return false;
            }
            var host = endpoint.IdnHost;
            var hosts = AllowedHosts is { Length: > 0 } ? AllowedHosts : DefaultAllowedHosts;
            return hosts.Any(allowed => allowed.StartsWith("*.", StringComparison.Ordinal)
                ? host.EndsWith(allowed[1..], StringComparison.OrdinalIgnoreCase)
                : string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
