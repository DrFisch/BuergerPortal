using BuergerPortal.BundId;
using System.Text;
using System.Text.RegularExpressions;
using static BuergerPortal.Tests.BundId.SamlTestIdp;

namespace BuergerPortal.Tests.BundId
{
    /// <summary>
    /// Prüfprotokoll für den SAML-Inspektor: jede Prüfung mit Soll und Ist, Attribut → Claim, angezeigte Response ohne
    /// wiederverwendbare Signatur und ohne volle bPK2.
    /// </summary>
    public class BundIdInspectionTests
    {
        private static readonly BundIdOptions Options = new()
        {
            SpEntityId = SpEntityId,
            AssertionConsumerServiceUrl = Acs.ToString(),
        };

        private static (BundIdInspection Inspection, string Xml) Inspect()
        {
            var samlResponse = CreateResponse();
            var config = CreateSpConfiguration();
            var response = Validate(samlResponse, config);
            var attributes = BundIdAttributes.FromSaml(response.ClaimsIdentity, Options.PostkorbHandleAttribute);
            var state = new BundIdLoginState(RequestId, TrustLevel.Normal, "/connect/authorize?client_id=mvc_web&state=abc123");
            var inspection = BundIdInspection.Create(response, config, state, attributes, Options, DateTime.UtcNow);
            return (inspection, Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse)));
        }

        [Fact]
        public void Protokoll_nennt_jede_Pruefung_mit_Soll_und_Ist()
        {
            var (inspection, _) = Inspect();

            Assert.Equal(
                ["Status", "Signatur", "Aussteller (Issuer)", "Zielgruppe (Audience)", "Empfänger (Destination, Recipient)",
                 "Zeitfenster", "Anfrage-Bezug (InResponseTo)", "Einmaligkeit (Replay)", "Vertrauensniveau", "Rücksprungziel"],
                inspection.Checks.Select(c => c.Name));
            Assert.All(inspection.Checks, c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Expected));
                Assert.False(string.IsNullOrWhiteSpace(c.Actual));
                Assert.False(string.IsNullOrWhiteSpace(c.CheckedBy));
            });
            Assert.Equal(TrustLevel.Substantial, inspection.ReachedLevel);
        }

        [Fact]
        public void Signatur_zeigt_Algorithmus_signiertes_Element_und_Zertifikat_aus_den_Metadaten()
        {
            var signature = Inspect().Inspection.Checks.Single(c => c.Name == "Signatur");

            Assert.Contains("rsa-sha256", signature.Actual);
            Assert.Contains("= die Assertion", signature.Actual);
            Assert.Contains("gleich dem aus den Metadaten", signature.Actual);
            Assert.Contains("Test-IdP", signature.Expected);
        }

        [Fact]
        public void Empfaenger_und_Anfrage_bezug_zeigen_die_Werte_aus_der_Response()
        {
            var checks = Inspect().Inspection.Checks;

            Assert.Contains($"Recipient: {Acs}", checks.Single(c => c.Name.StartsWith("Empfänger")).Actual);
            Assert.Contains($"signierte Bestätigung: {RequestId}", checks.Single(c => c.Name.StartsWith("Anfrage-Bezug")).Actual);
        }

        [Fact]
        public void Ruecksprungziel_ohne_Parameter_der_OIDC_Anfrage()
        {
            var target = Inspect().Inspection.Checks.Single(c => c.Name == "Rücksprungziel");

            Assert.Equal("/connect/authorize", target.Actual);
        }

        [Fact]
        public void Attribute_werden_ihren_Claims_zugeordnet_bPK2_gekuerzt()
        {
            var attributes = Inspect().Inspection.Attributes;

            var bpk2 = attributes.Single(a => a.Name == BundIdOids.Bpk2);
            Assert.Equal(BundIdClaimTypes.Bpk2, bpk2.Claim);
            Assert.Equal(["BUND…-T01"], bpk2.Values);
            var givenName = attributes.Single(a => a.Name == BundIdOids.GivenName);
            Assert.Equal(BundIdClaimTypes.GivenName, givenName.Claim);
            Assert.Equal(["Tina"], givenName.Values);
            Assert.Equal(BundIdClaimTypes.TrustLevel, attributes.Single(a => a.Name == BundIdOids.EidCitizenQaaLevel).Claim);
        }

        [Fact]
        public void Angezeigte_Response_ist_nicht_wiederverwendbar_und_zeigt_die_bPK2_nicht_vollstaendig()
        {
            var (inspection, original) = Inspect();
            var signatureValue = Regex.Match(original, "<SignatureValue>([^<]+)</SignatureValue>").Groups[1].Value;

            Assert.DoesNotContain(signatureValue, inspection.ResponseXml);
            Assert.Contains("gekürzt", inspection.ResponseXml);
            Assert.DoesNotContain("BUNDIDSIM-T01", inspection.ResponseXml);
            Assert.Contains("BUND…-T01", inspection.ResponseXml);
            Assert.Contains("Tina", inspection.ResponseXml);
            Assert.DoesNotContain("<?xml", inspection.ResponseXml);
        }
    }
}
