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
    public class BundIdController(IOptions<BundIdOptions> options) : Controller
    {
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
