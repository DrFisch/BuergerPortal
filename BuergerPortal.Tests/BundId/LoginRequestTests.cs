using BuergerPortal.BundId;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace BuergerPortal.Tests.BundId
{
    /// <summary>
    /// Prüft den AuthnRequest, den BundIdSamlService an die BundID schickt (ForceAuthn), und das Cookie mit dem
    /// Anmeldezustand (Pfad auch unter einem Basispfad wie /postfach).
    /// </summary>
    public class LoginRequestTests
    {
        private const string IdpEntityId = "https://bundid.example.test/saml/metadata";
        private const string SsoUrl = "https://bundid.example.test/saml";

        [Fact]
        public async Task ForceAuthn_steht_im_AuthnRequest_wenn_konfiguriert()
        {
            var (xml, _) = await StartLoginAsync(forceAuthn: true, pathBase: "");

            Assert.Contains("ForceAuthn=\"true\"", xml);
        }

        [Fact]
        public async Task Ohne_ForceAuthn_darf_die_BundID_ihre_Anmeldesitzung_nutzen()
        {
            var (xml, _) = await StartLoginAsync(forceAuthn: false, pathBase: "");

            Assert.DoesNotContain("ForceAuthn", xml);
        }

        [Theory]
        [InlineData("", "path=/bundid")]
        [InlineData("/postfach", "path=/postfach/bundid")]
        public async Task Anmeldezustand_Cookie_gilt_fuer_den_ACS_unter_dem_Basispfad(string pathBase, string expectedPath)
        {
            var (_, setCookie) = await StartLoginAsync(forceAuthn: false, pathBase: pathBase);

            Assert.Contains("bundid_login=", setCookie);
            Assert.Contains(expectedPath, setCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=none", setCookie, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Lernpfad_zeigt_genau_den_AuthnRequest_der_im_Formular_steht()
        {
            // Lernpfad Schritt 2: Anzeige und gesendete Nachricht müssen dieselben sein (AuthenticationServer/BundId/Lernpfad.cs)
            var service = CreateService(forceAuthn: true, pathBase: "");
            var binding = await service.PrepareLoginAsync(new DefaultHttpContext().Response, 3,
                "/connect/authorize?client_id=mvc_web&bpsim_lernpfad=1", CancellationToken.None);

            var gesendet = Encoding.UTF8.GetString(Convert.FromBase64String(AuthenticationServer.BundId.Lernpfad.SamlRequest(binding)));
            Assert.Equal(binding.XmlDocument.OuterXml, gesendet);

            var daten = AuthenticationServer.BundId.Lernpfad.Lies(binding.XmlDocument);
            Assert.Equal(SsoUrl, daten.Destination);
            Assert.Equal("https://sp.example.test/bundid/metadata", daten.Issuer);
            Assert.Equal("https://sp.example.test/bundid/acs", daten.AssertionConsumerServiceUrl);
            Assert.Equal("STORK-QAA-Level-3", daten.Level);
            Assert.True(daten.ForceAuthn);
            Assert.StartsWith("_", daten.Id);
            Assert.Contains("<samlp:AuthnRequest", AuthenticationServer.BundId.Lernpfad.Eingerueckt(binding.XmlDocument));
        }

        private static BundIdSamlService CreateService(bool forceAuthn, string pathBase)
        {
            var options = Options.Create(new BundIdOptions
            {
                SpEntityId = "https://sp.example.test/bundid/metadata",
                AssertionConsumerServiceUrl = "https://sp.example.test" + pathBase + "/bundid/acs",
                IdpMetadataUrl = IdpEntityId,
                ForceAuthn = forceAuthn,
            });
            var configuration = new BundIdSamlConfigurationProvider(options, new MetadataClientFactory(),
                new BundIdTokenReplayCache(), NullLogger<BundIdSamlConfigurationProvider>.Instance);
            return new BundIdSamlService(options, configuration,
                new BundIdLoginStateStore(new EphemeralDataProtectionProvider()), NullLogger<BundIdSamlService>.Instance);
        }

        private static async Task<(string Xml, string SetCookie)> StartLoginAsync(bool forceAuthn, string pathBase)
        {
            var options = Options.Create(new BundIdOptions
            {
                SpEntityId = "https://sp.example.test/bundid/metadata",
                AssertionConsumerServiceUrl = "https://sp.example.test" + pathBase + "/bundid/acs",
                IdpMetadataUrl = IdpEntityId,
                ForceAuthn = forceAuthn,
            });
            var configuration = new BundIdSamlConfigurationProvider(options, new MetadataClientFactory(),
                new BundIdTokenReplayCache(), NullLogger<BundIdSamlConfigurationProvider>.Instance);
            var service = new BundIdSamlService(options, configuration,
                new BundIdLoginStateStore(new EphemeralDataProtectionProvider()), NullLogger<BundIdSamlService>.Instance);

            var context = new DefaultHttpContext();
            context.Request.PathBase = pathBase;
            var result = (ContentResult)await service.StartLoginAsync(context.Response, 1, "/", CancellationToken.None);

            var samlRequest = Regex.Match(result.Content!, "name=\"SAMLRequest\" value=\"([^\"]+)\"").Groups[1].Value;
            var xml = Encoding.UTF8.GetString(Convert.FromBase64String(WebUtility.HtmlDecode(samlRequest)));
            return (xml, context.Response.Headers.SetCookie.ToString());
        }

        // Liefert IdP-Metadaten wie der Simulator unter /saml/metadata.
        private sealed class MetadataClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(new MetadataHandler());
        }

        private sealed class MetadataHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                using var rsa = RSA.Create(2048);
                using var certificate = new CertificateRequest("CN=Test-IdP", rsa, HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
                var metadata = $"""
                    <md:EntityDescriptor xmlns:md="urn:oasis:names:tc:SAML:2.0:metadata" xmlns:ds="http://www.w3.org/2000/09/xmldsig#" entityID="{IdpEntityId}">
                      <md:IDPSSODescriptor protocolSupportEnumeration="urn:oasis:names:tc:SAML:2.0:protocol">
                        <md:KeyDescriptor use="signing"><ds:KeyInfo><ds:X509Data><ds:X509Certificate>{Convert.ToBase64String(certificate.RawData)}</ds:X509Certificate></ds:X509Data></ds:KeyInfo></md:KeyDescriptor>
                        <md:SingleSignOnService Binding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST" Location="{SsoUrl}"/>
                      </md:IDPSSODescriptor>
                    </md:EntityDescriptor>
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata) });
            }
        }
    }
}
