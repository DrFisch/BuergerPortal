namespace BuergerPortal.Domain.Push.Entity
{
    /// <summary>
    /// Abo für Push-Benachrichtigungen (Web Push) eines Browsers bzw. einer installierten App. Der Browser liefert
    /// beim Abonnieren die Adresse beim Push-Dienst (Endpoint) und zwei Schlüssel für die Ende-zu-Ende-Verschlüsselung
    /// (RFC 8291). Eine Person kann mehrere Geräte haben; jede Adresse gehört genau einer Person.
    /// </summary>
    public class PushSubscription
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Person (sub aus dem Access-Token).</summary>
        public Guid UserId { get; set; }

        /// <summary>Adresse des Abos beim Push-Dienst (https://…), vom Browser vergeben.</summary>
        public string Endpoint { get; set; } = default!;

        /// <summary>Öffentlicher Schlüssel des Browsers (P-256, base64url).</summary>
        public string P256dh { get; set; } = default!;

        /// <summary>Auth-Geheimnis des Browsers (16 Byte, base64url).</summary>
        public string Auth { get; set; } = default!;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
