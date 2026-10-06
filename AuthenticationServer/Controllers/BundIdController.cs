using AuthenticationServer.BundId;
using BuergerPortal.BundId;
using AuthenticationServer.Data;
using AuthenticationServer.Models;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        BundIdSamlConfigurationProvider samlConfiguration, BundIdLoginStateStore loginStateStore,
        BundIdUserService userService, SignInManager<ApplicationUser> signInManager,
        ILogger<BundIdController> logger) : Controller
    {
        // Startet die Anmeldung: AuthnRequest an die BundID (HTTP-POST-Binding = Formular mit Auto-Submit).
        // level = gefordertes Mindest-Vertrauensniveau (STORK-QAA-Level 1, 3 oder 4),
        // returnUrl = wohin es nach erfolgreicher Anmeldung weitergeht (nur lokale Adressen).
        [HttpGet("login")]
        public async Task<IActionResult> Login(int? level, string? returnUrl, CancellationToken ct)
        {
            var bundId = options.Value;
            var requestedLevel = level is 1 or 3 or 4 ? level.Value : bundId.DefaultTrustLevel;
            // Schutz vor Open Redirect: externe Ziele werden durch die Startseite ersetzt.
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

            Saml2Configuration config;
            try
            {
                config = await samlConfiguration.GetConfigurationAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                logger.LogError(ex, "BundID-Metadaten nicht abrufbar");
                return LoginError("BundID nicht erreichbar",
                    "Die Anmeldung über die BundID ist gerade nicht möglich. Bitte versuchen Sie es später erneut.",
                    requestedLevel, safeReturnUrl);
            }

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
            // Gemerkten Anmeldezustand holen und sofort verbrauchen: jede Anfrage nur einmal beantwortbar.
            var loginState = loginStateStore.Read(Request);
            loginStateStore.Delete(Response);
            var retryLevel = loginState?.RequestedLevel ?? options.Value.DefaultTrustLevel;
            var returnUrl = loginState?.ReturnUrl ?? "/";

            try
            {
                var config = await samlConfiguration.GetConfigurationAsync(ct);
                var httpRequest = Request.ToGenericHttpRequest(validate: true);
                var authnResponse = new Saml2AuthnResponse(config);

                // Zuerst nur Status und InResponseTo lesen: Abbruch oder Fehler enthalten keine Assertion.
                httpRequest.Binding.ReadSamlResponse(httpRequest, authnResponse);

                // Die Antwort muss zu genau der Anfrage gehören, die dieser Browser gestartet hat.
                // ITfoxtec prüft das nicht selbst; ohne diese Prüfung würden auch fremde oder alte Antworten akzeptiert.
                if (loginState == null || authnResponse.InResponseToAsString != loginState.RequestId)
                {
                    logger.LogWarning("BundID-Response ohne passende offene Anfrage (InResponseTo {InResponseTo})",
                        authnResponse.InResponseToAsString);
                    return LoginError("Anmeldung abgelaufen",
                        "Diese Anmeldung ist abgelaufen oder wurde bereits verwendet. Bitte melden Sie sich erneut an.",
                        retryLevel, returnUrl);
                }

                if (authnResponse.Status != Saml2StatusCodes.Success)
                {
                    // Abbruch (oder Fehler) bei der BundID: zurück zum Portal, das die Anmeldung angefordert hat.
                    logger.LogInformation("BundID-Anmeldung nicht abgeschlossen: {Status} {Message}",
                        authnResponse.Status, authnResponse.StatusMessage);
                    return Cancel(returnUrl);
                }

                httpRequest.Binding.Unbind(httpRequest, authnResponse);

                // SAML-Attribute (OIDs) in verständliche Werte und Claims übersetzen.
                var attributes = BundIdAttributes.FromSaml(authnResponse.ClaimsIdentity, options.Value.PostkorbHandleAttribute);

                // Die BundID (bzw. der Simulator) erzwingt das geforderte Niveau nicht zuverlässig – selbst prüfen.
                if (attributes.TrustLevel < loginState.RequestedLevel)
                {
                    return LoginError("Vertrauensniveau zu niedrig",
                        $"Für diese Funktion ist das Vertrauensniveau „{TrustLevel.Describe(loginState.RequestedLevel)}“ nötig, " +
                        $"angemeldet wurde mit „{TrustLevel.Describe(attributes.TrustLevel)}“. Bitte melden Sie sich mit einem " +
                        "passenden Identifizierungsmittel an (z. B. ELSTER-Zertifikat oder Online-Ausweis).",
                        loginState.RequestedLevel, returnUrl);
                }

                // Konto über die bPK2 wiedererkennen oder beim ersten Login anlegen.
                var (user, created) = await userService.FindOrCreateAsync(attributes, ct);
                await userService.UpdateLoginDataAsync(user, attributes);

                // Anmeldung am Auth-Server: Identity-Sitzung mit den BundID-Daten als Claims (nur für diese Sitzung).
                await signInManager.SignInWithClaimsAsync(user, isPersistent: false, attributes.ToClaims());
                logger.LogInformation("BundID-Login: Konto {UserId} ({Status}), Niveau {Level}", user.Id,
                    created ? "neu" : "bekannt", attributes.TrustLevel);
                return LocalRedirect(loginState.ReturnUrl);
            }
            catch (BundIdException ex)
            {
                logger.LogWarning(ex, "BundID-Anmeldung abgelehnt");
                return LoginError("Anmeldung nicht möglich", ex.Message, retryLevel, returnUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Signatur ungültig, falscher Issuer/Audience, abgelaufen, bereits verwendet (Replay) usw.
                logger.LogWarning(ex, "Ungültige SAML-Response der BundID");
                return LoginError("Antwort der BundID ungültig",
                    "Die Antwort der BundID konnte nicht bestätigt werden. Bitte melden Sie sich erneut an.",
                    retryLevel, returnUrl);
            }
        }

        // Anmeldung abbrechen: Läuft gerade eine OIDC-Anmeldung eines Portals (/connect/authorize), erhält das Portal
        // den Fehler access_denied und kann selbst eine verständliche Seite zeigen. Sonst zur Startseite.
        [HttpGet("cancel")]
        public IActionResult Cancel(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                && returnUrl.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
            {
                // error_description darf laut RFC 6749 nur ASCII enthalten; den Text für Menschen zeigt das Portal.
                TempData[CancelledKey] = "Anmeldung bei der BundID abgebrochen";
                return LocalRedirect(returnUrl);
            }
            return LocalRedirect("/");
        }

        // Von AuthorizationController.Authorize gelesen.
        public const string CancelledKey = "BundIdCancelled";

        private ViewResult LoginError(string title, string message, int retryLevel, string returnUrl) =>
            View("Error", new BundIdErrorViewModel
            {
                Title = title,
                Message = message,
                RetryUrl = Url.Action(nameof(Login), new { level = retryLevel, returnUrl }) ?? "/bundid/login",
                CancelUrl = Url.Action(nameof(Cancel), new { returnUrl }) ?? "/",
            });

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
