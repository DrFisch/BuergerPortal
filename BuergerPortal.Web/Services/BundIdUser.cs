using BuergerPortal.BundId;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace BuergerPortal.Web.Services
{
    /// <summary>Anschrift aus der BundID (Claim "address", JSON nach OIDC).</summary>
    public sealed record BundIdAddress(string? StreetAddress, string? PostalCode, string? Locality, string? Country);

    /// <summary>
    /// Angaben der angemeldeten Person aus der BundID. Sie stammen aus dem ID-Token und leben nur in der
    /// Sitzung, nicht in einer Datenbank des Portals – bei jeder Anmeldung liefert die BundID sie neu.
    /// </summary>
    public sealed record BundIdUser
    {
        public string? GivenName { get; init; }
        public string? FamilyName { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? Email { get; init; }
        public DateOnly? Birthdate { get; init; }
        public string? PlaceOfBirth { get; init; }
        public string? BirthName { get; init; }
        public BundIdAddress? Address { get; init; }
        public string? PostkorbHandle { get; init; }
        public string? Bpk2 { get; init; }

        /// <summary>Identifizierungsmittel laut BundID (z. B. "eID", "Elster", "Benutzername").</summary>
        public string? IdentificationMethod { get; init; }

        /// <summary>Erreichtes Vertrauensniveau (1, 3, 4); 0 = unbekannt, z. B. bei einem alten lokalen Konto.</summary>
        public int TrustLevel { get; init; }

        public DateTime? LastLoginUtc { get; init; }

        /// <summary>Angemeldet über die BundID (ein altes lokales Konto hat keine bPK2).</summary>
        public bool IsBundIdLogin => !string.IsNullOrEmpty(Bpk2);

        /// <summary>
        /// bPK2 für die Anzeige gekürzt (Anfang und Ende): Die Person erkennt ihre Kennung wieder, ein Blick über
        /// die Schulter oder ein Bildschirmfoto gibt sie aber nicht vollständig preis.
        /// </summary>
        public string? MaskedBpk2 => Bpk2 switch
        {
            null => null,
            { Length: <= 8 } => new string('•', Bpk2.Length),
            _ => $"{Bpk2[..4]}…{Bpk2[^4..]}",
        };

        public static BundIdUser FromPrincipal(ClaimsPrincipal principal)
        {
            string? Get(string type) =>
                principal.FindFirst(type)?.Value is { Length: > 0 } value ? value : null;

            var givenName = Get(BundIdClaimTypes.GivenName);
            var familyName = Get(BundIdClaimTypes.FamilyName);
            return new BundIdUser
            {
                GivenName = givenName,
                FamilyName = familyName,
                DisplayName = Get("name")
                    ?? string.Join(' ', new[] { givenName, familyName }.Where(s => s != null)),
                Email = Get(BundIdClaimTypes.Email),
                Birthdate = DateOnly.TryParseExact(Get(BundIdClaimTypes.Birthdate), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthdate) ? birthdate : null,
                PlaceOfBirth = Get(BundIdClaimTypes.PlaceOfBirth),
                BirthName = Get(BundIdClaimTypes.BirthName),
                Address = ParseAddress(Get(BundIdClaimTypes.Address)),
                PostkorbHandle = Get(BundIdClaimTypes.PostkorbHandle),
                Bpk2 = Get(BundIdClaimTypes.Bpk2),
                IdentificationMethod = Get(BundIdClaimTypes.IdentificationMethod),
                TrustLevel = BundId.TrustLevel.Parse(Get(BundIdClaimTypes.TrustLevel)) ?? 0,
                LastLoginUtc = DateTime.TryParse(Get(BundIdClaimTypes.LastLogin), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lastLogin)
                    ? lastLogin : null,
            };
        }

        // OIDC-Adress-Claim: {"street_address": …, "postal_code": …, "locality": …, "country": …}
        private static BundIdAddress? ParseAddress(string? json)
        {
            if (json == null) return null;
            try
            {
                var root = JsonDocument.Parse(json).RootElement;
                string? Field(string name) =>
                    root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                        ? value.GetString() : null;
                var address = new BundIdAddress(Field("street_address"), Field("postal_code"), Field("locality"),
                    Field("country"));
                return address is { StreetAddress: null, PostalCode: null, Locality: null, Country: null } ? null : address;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
