using BuergerPortal.BundId;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using static BuergerPortal.Tests.BundId.SamlTestIdp;

namespace BuergerPortal.Tests.BundId
{
    /// <summary>
    /// Prüft die SAML-Prüfkonfiguration des Auth-Servers mit selbst erzeugten, signierten Responses
    /// (Identity Provider im Test: <see cref="SamlTestIdp"/>).
    /// </summary>
    public class ResponseValidationTests
    {
        [Fact]
        public void Gueltige_signierte_Response_wird_akzeptiert()
        {
            var response = Validate(CreateResponse());

            var attributes = BundIdAttributes.FromSaml(response.ClaimsIdentity, "urn:oid:2.5.4.18");
            Assert.Equal(RequestId, response.InResponseToAsString);
            Assert.Equal("BUNDIDSIM-T01", attributes.Bpk2);
            Assert.Equal(TrustLevel.Substantial, attributes.TrustLevel);
        }

        [Fact]
        public void Manipulierte_Response_wird_abgelehnt()
        {
            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(CreateResponse()));
            var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml.Replace("Tina", "Tino")));

            Assert.Throws<InvalidSignatureException>(() => Validate(tampered));
        }

        [Fact]
        public void Response_mit_fremdem_Schluessel_wird_abgelehnt()
        {
            Assert.Throws<InvalidSignatureException>(() => Validate(CreateResponse(signingCertificate: OtherCertificate)));
        }

        [Fact]
        public void Response_ohne_Signatur_wird_abgelehnt()
        {
            // Signierte Response erzeugen und die Signatur entfernen (wie beim Original-Simulator: unsigniert).
            var xml = new System.Xml.XmlDocument { PreserveWhitespace = true };
            xml.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(CreateResponse())));
            var signature = xml.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#")[0]!;
            signature.ParentNode!.RemoveChild(signature);
            var unsigned = Convert.ToBase64String(Encoding.UTF8.GetBytes(xml.OuterXml));

            Assert.Throws<InvalidSignatureException>(() => Validate(unsigned));
        }

        [Fact]
        public void Response_fuer_anderen_Service_Provider_wird_abgelehnt()
        {
            Assert.Throws<SecurityTokenInvalidAudienceException>(() => Validate(CreateResponse(audience: "https://anderer-dienst.example.test")));
        }

        [Fact]
        public void Response_von_anderem_Issuer_wird_abgelehnt()
        {
            Assert.Throws<Saml2RequestException>(() => Validate(CreateResponse(issuer: "https://falscher-idp.example.test")));
        }

        [Fact]
        public void Dieselbe_Response_wird_kein_zweites_Mal_akzeptiert()
        {
            var replayCache = new BundIdTokenReplayCache();
            var samlResponse = CreateResponse();
            Validate(samlResponse, replayCache);

            Assert.Throws<SecurityTokenReplayDetectedException>(() => Validate(samlResponse, replayCache));
        }

        [Fact]
        public void Abgelaufene_Response_wird_abgelehnt()
        {
            // Gültig signiert, aber vor einer Stunde ausgestellt und nur 5 Minuten gültig (mitgeschnittene alte Antwort).
            var expired = CreateResponse(issuedAgo: TimeSpan.FromHours(1));

            var error = Assert.Throws<Saml2RequestException>(() => Validate(expired));
            Assert.Contains("expired", error.Message);
        }

        [Fact]
        public void Gueltige_Response_ist_an_den_eigenen_ACS_und_die_eigene_Anfrage_gerichtet()
        {
            BundIdResponseChecks.EnsureAddressedTo(Validate(CreateResponse()), Acs, RequestId);
        }

        [Fact]
        public void ITfoxtec_prueft_Destination_und_Recipient_nicht_selbst()
        {
            // Beleg, warum BundIdResponseChecks nötig ist: Die Bibliothek akzeptiert eine Antwort an einen fremden ACS.
            var response = Validate(CreateResponse(destination: new Uri("https://fremder-dienst.example.test/acs")));

            Assert.Equal("https://fremder-dienst.example.test/acs", response.Destination!.OriginalString);
        }

        [Fact]
        public void Response_an_fremden_ACS_wird_abgelehnt()
        {
            var response = Validate(CreateResponse(destination: new Uri("https://fremder-dienst.example.test/acs")));

            Assert.Throws<BundIdInvalidResponseException>(() => BundIdResponseChecks.EnsureAddressedTo(response, Acs, RequestId));
        }

        [Fact]
        public void Signierte_Bestaetigung_fuer_eine_andere_Anfrage_wird_abgelehnt()
        {
            // Angriff: eigene, gültig signierte Assertion (Anfrage des Angreifers) in eine Response packen, deren
            // unsigniertes InResponseTo auf die Anfrage des Opfers zeigt. Die Signatur bleibt gültig.
            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(CreateResponse(requestId: "_req-angreifer")));
            var document = new System.Xml.XmlDocument { PreserveWhitespace = true };
            document.LoadXml(xml);
            document.DocumentElement!.SetAttribute("InResponseTo", RequestId);
            var response = Validate(Convert.ToBase64String(Encoding.UTF8.GetBytes(document.OuterXml)));

            Assert.Equal(RequestId, response.InResponseToAsString);
            Assert.Throws<BundIdInvalidResponseException>(() => BundIdResponseChecks.EnsureAddressedTo(response, Acs, RequestId));
        }
    }
}
