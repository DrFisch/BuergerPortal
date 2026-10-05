using System.Security.Claims;
using System.Text.Json;

namespace AuthenticationServer.BundId
{
    /// <summary>SAML-Attributnamen (OIDs) der BundID, wie sie in der Assertion stehen.</summary>
    public static class BundIdOids
    {
        public const string Bpk2 = "urn:oid:1.3.6.1.4.1.25484.494450.3";
        public const string GivenName = "urn:oid:2.5.4.42";
        public const string Surname = "urn:oid:2.5.4.4";
        public const string Mail = "urn:oid:0.9.2342.19200300.100.1.3";
        public const string Birthdate = "urn:oid:1.2.40.0.10.2.1.1.55";
        public const string PlaceOfBirth = "urn:oid:1.3.6.1.5.5.7.9.2";
        public const string BirthName = "urn:oid:1.2.40.0.10.2.1.1.225566";
        public const string PostalAddress = "urn:oid:2.5.4.16";
        public const string PostalCode = "urn:oid:2.5.4.17";
        public const string LocalityName = "urn:oid:2.5.4.7";
        public const string Country = "urn:oid:1.2.40.0.10.2.1.1.225599";
        public const string EidCitizenQaaLevel = "urn:oid:1.2.40.0.10.2.1.1.261.94";
        public const string AssertionProvedBy = "urn:oid:1.3.6.1.4.1.25484.494450.2";
    }

    /// <summary>Namen der Claims, unter denen das Portal die BundID-Daten weitergibt (soweit möglich OIDC-Standard).</summary>
    public static class BundIdClaimTypes
    {
        public const string GivenName = "given_name";
        public const string FamilyName = "family_name";
        public const string Email = "email";
        public const string Birthdate = "birthdate";
        public const string Address = "address";
        public const string PlaceOfBirth = "bundid_place_of_birth";
        public const string BirthName = "bundid_birth_name";
        public const string Bpk2 = "bundid_bpk2";
        public const string PostkorbHandle = "postkorb_handle";
        public const string TrustLevel = "acr";                 // STORK-QAA-Level-n
        public const string IdentificationMethod = "amr";       // z. B. eID, Elster, Benutzername
    }

    /// <summary>Die übermittelten BundID-Daten einer Anmeldung, aus den SAML-Attributen gelesen.</summary>
    public sealed record BundIdAttributes
    {
        public required string Bpk2 { get; init; }
        public string? GivenName { get; init; }
        public string? FamilyName { get; init; }
        public string? Email { get; init; }
        public string? Birthdate { get; init; }
        public string? PlaceOfBirth { get; init; }
        public string? BirthName { get; init; }
        public string? StreetAddress { get; init; }
        public string? PostalCode { get; init; }
        public string? Locality { get; init; }
        public string? Country { get; init; }
        public string? PostkorbHandle { get; init; }
        public string? QaaLevelAttribute { get; init; }         // Attribut EID-CITIZEN-QAA-LEVEL
        public string? IdentificationMethod { get; init; }      // Attribut AssertionProvedBy

        /// <summary>
        /// Liest die Attribute aus der geprüften Assertion. OID-Namen werden ohne Leerzeichen verglichen
        /// (der Original-Simulator schreibt z. B. "urn:oid: 1.2.40…"). Ohne bPK2 ist keine Zuordnung möglich.
        /// </summary>
        public static BundIdAttributes FromSaml(ClaimsIdentity samlIdentity, string postkorbHandleAttribute)
        {
            var values = samlIdentity.Claims
                .GroupBy(c => Normalize(c.Type))
                .ToDictionary(g => g.Key, g => g.First().Value);
            string? Get(string oid) => values.TryGetValue(Normalize(oid), out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

            return new BundIdAttributes
            {
                Bpk2 = Get(BundIdOids.Bpk2) ?? throw new BundIdException("Die BundID hat keine bPK2 übermittelt."),
                GivenName = Get(BundIdOids.GivenName),
                FamilyName = Get(BundIdOids.Surname),
                Email = Get(BundIdOids.Mail),
                Birthdate = Get(BundIdOids.Birthdate),
                PlaceOfBirth = Get(BundIdOids.PlaceOfBirth),
                BirthName = Get(BundIdOids.BirthName),
                StreetAddress = Get(BundIdOids.PostalAddress),
                PostalCode = Get(BundIdOids.PostalCode),
                Locality = Get(BundIdOids.LocalityName),
                Country = Get(BundIdOids.Country),
                PostkorbHandle = Get(postkorbHandleAttribute),
                QaaLevelAttribute = Get(BundIdOids.EidCitizenQaaLevel),
                IdentificationMethod = Get(BundIdOids.AssertionProvedBy),
            };
        }

        /// <summary>Vor- und Nachname für die Anzeige.</summary>
        public string DisplayName =>
            string.Join(' ', new[] { GivenName, FamilyName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>Claims für die Sitzung und die Tokens (ohne leere Werte).</summary>
        public IEnumerable<Claim> ToClaims()
        {
            var claims = new List<Claim> { new(BundIdClaimTypes.Bpk2, Bpk2) };
            void Add(string type, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) claims.Add(new Claim(type, value));
            }

            Add(BundIdClaimTypes.GivenName, GivenName);
            Add(BundIdClaimTypes.FamilyName, FamilyName);
            Add(BundIdClaimTypes.Email, Email);
            Add(BundIdClaimTypes.Birthdate, Birthdate);
            Add(BundIdClaimTypes.PlaceOfBirth, PlaceOfBirth);
            Add(BundIdClaimTypes.BirthName, BirthName);
            Add(BundIdClaimTypes.PostkorbHandle, PostkorbHandle);
            Add(BundIdClaimTypes.IdentificationMethod, IdentificationMethod);
            if (StreetAddress != null || PostalCode != null || Locality != null || Country != null)
            {
                // Adresse als JSON-Objekt wie im OIDC-Standardclaim "address".
                claims.Add(new Claim(BundIdClaimTypes.Address, JsonSerializer.Serialize(new
                {
                    street_address = StreetAddress,
                    postal_code = PostalCode,
                    locality = Locality,
                    country = Country,
                })));
            }
            return claims;
        }

        private static string Normalize(string oid) => string.Concat(oid.Where(c => !char.IsWhiteSpace(c)));
    }

    /// <summary>Fachlicher Fehler bei der BundID-Anmeldung (Meldung ist für Bürgerinnen und Bürger verständlich).</summary>
    public sealed class BundIdException(string message) : Exception(message);
}
