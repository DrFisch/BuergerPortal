using AuthenticationServer.BundId;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuthenticationServer.Controllers
{
    /// <summary>
    /// Anmeldung über die BundID (SAML 2.0). Der Auth-Server ist dabei Service Provider (SP),
    /// die BundID bzw. der Simulator ist Identity Provider (IdP).
    /// </summary>
    [AllowAnonymous]
    [Route("bundid")]
    public class BundIdController(IOptions<BundIdOptions> options,
        BundIdSamlConfigurationProvider samlConfiguration) : Controller
    {
        // Startet die Anmeldung: AuthnRequest an die BundID (HTTP-POST-Binding = Formular mit Auto-Submit).
        // level = gefordertes Mindest-Vertrauensniveau (STORK-QAA-Level 1, 3 oder 4).
        [HttpGet("login")]
        public async Task<IActionResult> Login(int? level, CancellationToken ct)
        {
            var bundId = options.Value;
            var config = await samlConfiguration.GetConfigurationAsync(ct);
            var requestedLevel = level is 1 or 3 or 4 ? level.Value : bundId.DefaultTrustLevel;

            var authnRequest = new Saml2AuthnRequest(config)
            {
                AssertionConsumerServiceUrl = new Uri(bundId.AssertionConsumerServiceUrl),
                ProtocolBinding = ProtocolBindings.HttpPost,
                // Die BundID erwartet das Niveau als STORK-Bezeichner, Vergleich "minimum".
                RequestedAuthnContext = new RequestedAuthnContext
                {
                    Comparison = AuthnContextComparisonTypes.Minimum,
                    AuthnContextClassRef = [$"STORK-QAA-Level-{requestedLevel}"],
                },
                // Angeforderte Attribute und Anzeigename für die BundID-Seite.
                Extensions = AkdbExtension.Create(bundId),
            };
            return new Saml2PostBinding().Bind(authnRequest).ToActionResult();
        }

        // SP-Metadaten: Damit kann die BundID den Auth-Server als Service Provider einrichten
        // (EntityID, Adresse des Assertion Consumer Service, Binding, NameID-Format).
        [HttpGet("metadata")]
        public IActionResult Metadata()
        {
            var bundId = options.Value;
            var entityDescriptor = new EntityDescriptor(new Saml2Configuration { Issuer = bundId.SpEntityId })
            {
                ValidUntil = 365,
                SPSsoDescriptor = new SPSsoDescriptor
                {
                    AuthnRequestsSigned = false,
                    WantAssertionsSigned = true,
                    NameIDFormats = [NameIdentifierFormats.Transient],
                    AssertionConsumerServices =
                    [
                        new AssertionConsumerService
                        {
                            Binding = ProtocolBindings.HttpPost,
                            Location = new Uri(bundId.AssertionConsumerServiceUrl),
                        },
                    ],
                },
            };
            return new Saml2Metadata(entityDescriptor).CreateMetadata().ToActionResult();
        }
    }
}
