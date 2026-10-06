using BuergerPortal.BundId;
using BuergerPortal.Web.Services;
using System.Security.Claims;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Lesen der BundID-Angaben aus den Claims der Portal-Sitzung.</summary>
    public class BundIdUserTests
    {
        private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
            new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "oidc", "name", null));

        [Fact]
        public void Liest_alle_BundID_Angaben()
        {
            var user = BundIdUser.FromPrincipal(Principal(
                ("name", "Manfred Schmidt"),
                (BundIdClaimTypes.GivenName, "Manfred"),
                (BundIdClaimTypes.FamilyName, "Schmidt"),
                (BundIdClaimTypes.Email, "m-schmidt@example.com"),
                (BundIdClaimTypes.Birthdate, "1969-02-11"),
                (BundIdClaimTypes.Address, """{"street_address":"Rankestr. 13","postal_code":"90461","locality":"Nürnberg","country":"DE"}"""),
                (BundIdClaimTypes.PostkorbHandle, "11b2dc8f-3831-3b26-afde-aa0be42bd79b"),
                (BundIdClaimTypes.Bpk2, "BUNDIDSIM-U02-bp"),
                (BundIdClaimTypes.IdentificationMethod, "Elster"),
                (BundIdClaimTypes.TrustLevel, "STORK-QAA-Level-3"),
                (BundIdClaimTypes.LastLogin, "2026-10-06T07:30:00.0000000Z")));

            Assert.Equal("Manfred Schmidt", user.DisplayName);
            Assert.Equal(new DateOnly(1969, 2, 11), user.Birthdate);
            Assert.Equal(new BundIdAddress("Rankestr. 13", "90461", "Nürnberg", "DE"), user.Address);
            Assert.Equal("Elster", user.IdentificationMethod);
            Assert.Equal(TrustLevel.Substantial, user.TrustLevel);
            Assert.Equal(new DateTime(2026, 10, 6, 7, 30, 0, DateTimeKind.Utc), user.LastLoginUtc);
            Assert.Equal(DateTimeKind.Utc, user.LastLoginUtc!.Value.Kind);
            Assert.True(user.IsBundIdLogin);
        }

        [Fact]
        public void Fehlende_Angaben_bleiben_leer()
        {
            // Altes lokales Konto: nur Name, kein Niveau, keine bPK2.
            var user = BundIdUser.FromPrincipal(Principal(("name", "alter.benutzer")));

            Assert.Equal("alter.benutzer", user.DisplayName);
            Assert.Null(user.Address);
            Assert.Null(user.Birthdate);
            Assert.Equal(0, user.TrustLevel);
            Assert.False(user.IsBundIdLogin);
        }

        [Fact]
        public void Name_aus_Vor_und_Nachname_wenn_name_fehlt()
        {
            var user = BundIdUser.FromPrincipal(Principal(
                (BundIdClaimTypes.GivenName, "Nils"), (BundIdClaimTypes.FamilyName, "Karlsson")));

            Assert.Equal("Nils Karlsson", user.DisplayName);
        }

        [Theory]
        [InlineData("kein json")]
        [InlineData("{}")]
        [InlineData("""{"street_address": 13}""")]
        public void Unbrauchbare_Adresse_wird_ignoriert(string json)
        {
            Assert.Null(BundIdUser.FromPrincipal(Principal((BundIdClaimTypes.Address, json))).Address);
        }

        [Fact]
        public void Versteht_auch_die_eIDAS_Schreibweise_des_Niveaus()
        {
            var user = BundIdUser.FromPrincipal(Principal((BundIdClaimTypes.TrustLevel, "http://eidas.europa.eu/LoA/high")));

            Assert.Equal(TrustLevel.High, user.TrustLevel);
        }
    }
}
