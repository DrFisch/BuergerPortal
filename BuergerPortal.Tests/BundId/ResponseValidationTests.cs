using BuergerPortal.BundId;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Cryptography;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens.Saml2;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SamlHttpRequest = ITfoxtec.Identity.Saml2.Http.HttpRequest;

namespace BuergerPortal.Tests.BundId
{
    /// <summary>
    /// Prüft die SAML-Prüfkonfiguration des Auth-Servers mit selbst erzeugten, signierten Responses.
    /// ITfoxtec spielt dabei im Test den Identity Provider (BundID).
    /// </summary>
    public class ResponseValidationTests
    {
        private const string SpEntityId = "https://auth.example.test/bundid/metadata";
        private const string IdpEntityId = "https://bundid.example.test/saml/metadata";
        private const string RequestId = "_req-0001";
        private static readonly Uri Acs = new("https://auth.example.test/bundid/acs");
        private static readonly X509Certificate2 IdpCertificate = CreateCertificate("CN=Test-IdP");
        private static readonly X509Certificate2 OtherCertificate = CreateCertificate("CN=Fremder IdP");

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

        // Service-Provider-Seite: dieselbe Konfiguration wie im Auth-Server.
        private static Saml2AuthnResponse Validate(string samlResponse, BundIdTokenReplayCache? replayCache = null)
        {
            var config = BundIdSamlConfigurationProvider.CreateConfiguration(SpEntityId, IdpEntityId,
                new Uri("https://bundid.example.test/saml"), [IdpCertificate], replayCache ?? new BundIdTokenReplayCache());
            var response = new Saml2AuthnResponse(config);
            var request = new SamlHttpRequest
            {
                Method = "POST",
                Form = new NameValueCollection { { "SAMLResponse", samlResponse } },
            };
            new Saml2PostBinding().Unbind(request, response);
            return response;
        }

        // Identity-Provider-Seite: signierte Response wie vom Simulator-Fork (Assertion signiert).
        private static string CreateResponse(X509Certificate2? signingCertificate = null,
            string audience = SpEntityId, string issuer = IdpEntityId, Uri? destination = null, string requestId = RequestId)
        {
            var idpConfig = new Saml2Configuration
            {
                Issuer = issuer,
                SigningCertificate = signingCertificate ?? IdpCertificate,
                AuthnResponseSignType = Saml2AuthnResponseSignTypes.SignAssertion,
            };
            var response = new Saml2AuthnResponse(idpConfig)
            {
                InResponseTo = new Saml2Id(requestId),
                Status = Saml2StatusCodes.Success,
                // ITfoxtec übernimmt die Destination auch als Recipient der Bearer-Bestätigung.
                Destination = destination ?? Acs,
                NameId = new Saml2NameIdentifier("BUNDIDSIM-T01", NameIdentifierFormats.Transient),
                ClaimsIdentity = new ClaimsIdentity(
                [
                    new Claim(BundIdOids.Bpk2, "BUNDIDSIM-T01"),
                    new Claim(BundIdOids.GivenName, "Tina"),
                    new Claim(BundIdOids.Surname, "Test"),
                    new Claim(BundIdOids.EidCitizenQaaLevel, "STORK-QAA-Level-3"),
                ]),
            };
            response.CreateSecurityToken(audience, authnContext: new Uri("http://eidas.europa.eu/LoA/substantial"));

            var binding = new Saml2PostBinding();
            binding.Bind(response);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(binding.XmlDocument.OuterXml));
        }

        private static X509Certificate2 CreateCertificate(string subject)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        }
    }
}
