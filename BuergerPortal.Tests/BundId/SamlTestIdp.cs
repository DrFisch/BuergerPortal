using BuergerPortal.BundId;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas;
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
    /// Testhilfe: ITfoxtec spielt den Identity Provider (BundID) und erzeugt signierte Responses wie der Simulator-Fork;
    /// <see cref="Validate"/> prüft sie mit genau der Konfiguration des Auth-Servers.
    /// </summary>
    internal static class SamlTestIdp
    {
        public const string SpEntityId = "https://auth.example.test/bundid/metadata";
        public const string IdpEntityId = "https://bundid.example.test/saml/metadata";
        public const string RequestId = "_req-0001";
        public static readonly Uri Acs = new("https://auth.example.test/bundid/acs");
        public static readonly X509Certificate2 IdpCertificate = CreateCertificate("CN=Test-IdP");
        public static readonly X509Certificate2 OtherCertificate = CreateCertificate("CN=Fremder IdP");

        public static Saml2Configuration CreateSpConfiguration(BundIdTokenReplayCache? replayCache = null) =>
            BundIdSamlConfigurationProvider.CreateConfiguration(SpEntityId, IdpEntityId,
                new Uri("https://bundid.example.test/saml"), [IdpCertificate], replayCache ?? new BundIdTokenReplayCache());

        // Service-Provider-Seite: dieselbe Konfiguration wie im Auth-Server.
        public static Saml2AuthnResponse Validate(string samlResponse, BundIdTokenReplayCache? replayCache = null) =>
            Validate(samlResponse, CreateSpConfiguration(replayCache));

        public static Saml2AuthnResponse Validate(string samlResponse, Saml2Configuration config)
        {
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
        public static string CreateResponse(X509Certificate2? signingCertificate = null,
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
