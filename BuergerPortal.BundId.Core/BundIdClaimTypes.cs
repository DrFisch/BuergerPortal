namespace BuergerPortal.BundId
{
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
        public const string LastLogin = "bundid_last_login";    // aus der DB (Zeitpunkt der Anmeldung)

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            GivenName, FamilyName, Email, Birthdate, Address, PlaceOfBirth, BirthName, Bpk2, PostkorbHandle,
            TrustLevel, IdentificationMethod,
        };
    }
}
