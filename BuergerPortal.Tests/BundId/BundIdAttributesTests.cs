using AuthenticationServer.BundId;
using System.Security.Claims;
using System.Text.Json;

namespace BuergerPortal.Tests.BundId
{
    public class BundIdAttributesTests
    {
        private const string PostkorbOid = "urn:oid:2.5.4.18";

        // So legt ITfoxtec die Assertion ab: Attribute mit ihrem Namen (OID), ClassRef als authenticationmethod.
        private static ClaimsIdentity Saml(params (string Type, string Value)[] claims) =>
            new(claims.Select(c => new Claim(c.Type, c.Value)), "saml");

        private static (string, string)[] FullAssertion() =>
        [
            (ClaimTypes.AuthenticationMethod, "http://eidas.europa.eu/LoA/substantial"),
            (ClaimTypes.AuthenticationMethod, "urn:oasis:names:tc:SAML:2.0:ac:classes:Password"),
            (BundIdOids.Bpk2, "BUNDIDSIM-U02-bp"),
            (BundIdOids.GivenName, "Manfred"),
            (BundIdOids.Surname, "Schmidt"),
            (BundIdOids.Mail, "m-schmidt-bp@example.com"),
            (BundIdOids.Birthdate, "1969-02-11"),
            (BundIdOids.PostalAddress, "Rankestr. 13"),
            (BundIdOids.PostalCode, "90461"),
            (BundIdOids.LocalityName, "Nürnberg"),
            (BundIdOids.Country, "DE"),
            (BundIdOids.EidCitizenQaaLevel, "STORK-QAA-Level-3"),
            (BundIdOids.AssertionProvedBy, "Elster"),
            (PostkorbOid, "11b2dc8f-3831-3b26-afde-aa0be42bd79b"),
        ];

        [Fact]
        public void FromSaml_bildet_alle_Attribute_ab()
        {
            var attributes = BundIdAttributes.FromSaml(Saml(FullAssertion()), PostkorbOid);

            Assert.Equal("BUNDIDSIM-U02-bp", attributes.Bpk2);
            Assert.Equal("Manfred Schmidt", attributes.DisplayName);
            Assert.Equal("m-schmidt-bp@example.com", attributes.Email);
            Assert.Equal("1969-02-11", attributes.Birthdate);
            Assert.Equal("Nürnberg", attributes.Locality);
            Assert.Equal("Elster", attributes.IdentificationMethod);
            Assert.Equal("11b2dc8f-3831-3b26-afde-aa0be42bd79b", attributes.PostkorbHandle);
            Assert.Equal(TrustLevel.Substantial, attributes.TrustLevel);
        }

        [Fact]
        public void FromSaml_akzeptiert_OIDs_mit_Leerzeichen_wie_im_Original_Simulator()
        {
            var attributes = BundIdAttributes.FromSaml(Saml(
                (ClaimTypes.AuthenticationMethod, "STORK-QAA-Level-1"),
                ("urn:oid:1.3.6.1.4.1.25484.494450.3", "BUNDIDSIM-U01"),
                ("urn:oid: 1.2.40.0.10.2.1.1.261.94", "STORK-QAA-Level-1")), PostkorbOid);

            Assert.Equal("BUNDIDSIM-U01", attributes.Bpk2);
            Assert.Equal("STORK-QAA-Level-1", attributes.QaaLevelAttribute);
        }

        [Fact]
        public void FromSaml_ohne_bPK2_wird_abgelehnt()
        {
            var ex = Assert.Throws<BundIdException>(() => BundIdAttributes.FromSaml(
                Saml((ClaimTypes.AuthenticationMethod, "STORK-QAA-Level-1"), (BundIdOids.GivenName, "Maria")), PostkorbOid));
            Assert.Contains("bPK2", ex.Message);
        }

        [Fact]
        public void FromSaml_ohne_Vertrauensniveau_wird_abgelehnt()
        {
            Assert.Throws<BundIdException>(() => BundIdAttributes.FromSaml(
                Saml((BundIdOids.Bpk2, "BUNDIDSIM-U01")), PostkorbOid));
        }

        [Fact]
        public void FromSaml_nimmt_bei_widerspruechlichen_Angaben_das_niedrigere_Niveau()
        {
            var attributes = BundIdAttributes.FromSaml(Saml(
                (ClaimTypes.AuthenticationMethod, "http://eidas.europa.eu/LoA/high"),
                (BundIdOids.Bpk2, "BUNDIDSIM-U01"),
                (BundIdOids.EidCitizenQaaLevel, "STORK-QAA-Level-1")), PostkorbOid);

            Assert.Equal(TrustLevel.Normal, attributes.TrustLevel);
        }

        [Fact]
        public void FromSaml_liest_das_Postkorb_Handle_aus_dem_konfigurierten_Attribut()
        {
            var attributes = BundIdAttributes.FromSaml(Saml(
                (ClaimTypes.AuthenticationMethod, "STORK-QAA-Level-1"),
                (BundIdOids.Bpk2, "BUNDIDSIM-U01"),
                ("urn:example:postkorb", "abc")), "urn:example:postkorb");

            Assert.Equal("abc", attributes.PostkorbHandle);
        }

        [Fact]
        public void ToClaims_enthaelt_Niveau_bPK2_und_Adresse_als_JSON()
        {
            var claims = BundIdAttributes.FromSaml(Saml(FullAssertion()), PostkorbOid).ToClaims().ToList();

            Assert.Contains(claims, c => c.Type == BundIdClaimTypes.TrustLevel && c.Value == "STORK-QAA-Level-3");
            Assert.Contains(claims, c => c.Type == BundIdClaimTypes.Bpk2 && c.Value == "BUNDIDSIM-U02-bp");
            Assert.Contains(claims, c => c.Type == BundIdClaimTypes.PostkorbHandle);
            var address = JsonDocument.Parse(claims.Single(c => c.Type == BundIdClaimTypes.Address).Value).RootElement;
            Assert.Equal("Rankestr. 13", address.GetProperty("street_address").GetString());
            Assert.Equal("Nürnberg", address.GetProperty("locality").GetString());
        }

        [Fact]
        public void ToClaims_laesst_fehlende_Angaben_weg()
        {
            // Wie Testperson U11: nur Name, Mail, Geburtsdatum – keine Adresse.
            var claims = BundIdAttributes.FromSaml(Saml(
                (ClaimTypes.AuthenticationMethod, "STORK-QAA-Level-1"),
                (BundIdOids.Bpk2, "BUNDIDSIM-U11-bp"),
                (BundIdOids.GivenName, "Nils"),
                (BundIdOids.Surname, "Karlsson")), PostkorbOid).ToClaims().ToList();

            Assert.DoesNotContain(claims, c => c.Type == BundIdClaimTypes.Address);
            Assert.DoesNotContain(claims, c => c.Type == BundIdClaimTypes.Email);
            Assert.DoesNotContain(claims, c => string.IsNullOrEmpty(c.Value));
        }
    }
}
