using AuthenticationServer.BundId;
using AuthenticationServer.Models;
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
        BundIdSamlConfigurationProvider samlConfiguration, BundIdLoginStateStore loginStateStore) : Controller
    {
        // Startet die Anmeldung: AuthnRequest an die BundID (HTTP-POST-Binding = Formular mit Auto-Submit).
        // level = gefordertes Mindest-Vertrauensniveau (STORK-QAA-Level 1, 3 oder 4),
        // returnUrl = wohin es nach erfolgreicher Anmeldung weitergeht (nur lokale Adressen).
        [HttpGet("login")]
        public async Task<IActionResult> Login(int? level, string? returnUrl, CancellationToken ct)
        {
            var bundId = options.Value;
            var config = await samlConfiguration.GetConfigurationAsync(ct);
            var requestedLevel = level is 1 or 3 or 4 ? level.Value : bundId.DefaultTrustLevel;
            // Schutz vor Open Redirect: externe Ziele werden durch die Startseite ersetzt.
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

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
            var binding = new Saml2PostBinding().Bind(authnRequest);

            // Für die Prüfung der Response merken: Nur eine Antwort auf genau diesen Request wird akzeptiert.
            loginStateStore.Save(Response, new BundIdLoginState(authnRequest.IdAsString, requestedLevel, safeReturnUrl));
            return binding.ToActionResult();
        }

        // Assertion Consumer Service: Hier kommt die SAML-Response der BundID an (HTTP-POST vom Browser).
        // ITfoxtec prüft beim Unbind die Signatur (Zertifikat aus den IdP-Metadaten), den Issuer,
        // die Audience (= eigene EntityID) und das Zeitfenster der Assertion.
        [HttpPost("acs")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AssertionConsumerService(CancellationToken ct)
        {
            var config = await samlConfiguration.GetConfigurationAsync(ct);
            var httpRequest = Request.ToGenericHttpRequest(validate: true);
            var authnResponse = new Saml2AuthnResponse(config);

            // Zuerst nur den Status lesen: Abbruch oder Fehler bei der BundID enthalten keine Assertion.
            httpRequest.Binding.ReadSamlResponse(httpRequest, authnResponse);
            if (authnResponse.Status != Saml2StatusCodes.Success)
            {
                return View("Result", new BundIdResultViewModel
                {
                    Status = authnResponse.Status.ToString(),
                    Message = authnResponse.StatusMessage,
                });
            }

            httpRequest.Binding.Unbind(httpRequest, authnResponse);
            return View("Result", new BundIdResultViewModel
            {
                Success = true,
                Status = authnResponse.Status.ToString(),
                Claims = authnResponse.ClaimsIdentity.Claims
                    .Select(c => new KeyValuePair<string, string>(c.Type, c.Value)).ToList(),
            });
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
