using AuthenticationServer.BundId;
using BuergerPortal.BundId;
using AuthenticationServer.Data;
using AuthenticationServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationServer.Controllers
{
    /// <summary>
    /// Anmeldung über die BundID (SAML 2.0). Der Auth-Server ist dabei Service Provider (SP),
    /// die BundID bzw. der Simulator ist Identity Provider (IdP). Den SAML-Ablauf selbst
    /// (Request, Prüfung der Response, Metadaten) liefert BundIdSamlService aus BuergerPortal.BundId.
    /// </summary>
    [AllowAnonymous]
    [Route("bundid")]
    public class BundIdController(BundIdSamlService saml, BundIdUserService userService,
        SignInManager<ApplicationUser> signInManager, ILogger<BundIdController> logger) : Controller
    {
        // Startet die Anmeldung: AuthnRequest an die BundID (HTTP-POST-Binding = Formular mit Auto-Submit).
        // level = gefordertes Mindest-Vertrauensniveau (STORK-QAA-Level 1, 3 oder 4),
        // returnUrl = wohin es nach erfolgreicher Anmeldung weitergeht (nur lokale Adressen).
        [HttpGet("login")]
        public async Task<IActionResult> Login(int? level, string? returnUrl, CancellationToken ct)
        {
            var requestedLevel = saml.NormalizeLevel(level);
            // Schutz vor Open Redirect: externe Ziele werden durch die Startseite ersetzt.
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

            try
            {
                return await saml.StartLoginAsync(Response, requestedLevel, safeReturnUrl, ct);
            }
            catch (BundIdUnavailableException)
            {
                return LoginError(BundIdUnavailableException.Title, BundIdUnavailableException.UserMessage,
                    requestedLevel, safeReturnUrl);
            }
        }

        // Assertion Consumer Service: Hier kommt die SAML-Response der BundID an (HTTP-POST vom Browser).
        [HttpPost("acs")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AssertionConsumerService(CancellationToken ct)
        {
            var result = await saml.ProcessResponseAsync(Request, Response, ct);
            switch (result.Outcome)
            {
                case BundIdLoginOutcome.Cancelled:
                    // Abbruch bei der BundID: zurück zum Portal, das die Anmeldung angefordert hat.
                    return Cancel(result.ReturnUrl);
                case BundIdLoginOutcome.Failed:
                    return LoginError(result.ErrorTitle, result.ErrorMessage, result.RequestedLevel, result.ReturnUrl);
            }

            var attributes = result.Attributes!;
            try
            {
                // Konto über die bPK2 wiedererkennen oder beim ersten Login anlegen.
                var (user, created) = await userService.FindOrCreateAsync(attributes, ct);
                await userService.UpdateLoginDataAsync(user, attributes);

                // Anmeldung am Auth-Server: Identity-Sitzung mit den BundID-Daten als Claims (nur für diese Sitzung).
                await signInManager.SignInWithClaimsAsync(user, isPersistent: false, attributes.ToClaims());
                logger.LogInformation("BundID-Login: Konto {UserId} ({Status}), Niveau {Level}", user.Id,
                    created ? "neu" : "bekannt", attributes.TrustLevel);
            }
            catch (BundIdException ex)
            {
                logger.LogWarning(ex, "BundID-Anmeldung abgelehnt");
                return LoginError("Anmeldung nicht möglich", ex.Message, result.RequestedLevel, result.ReturnUrl);
            }
            return LocalRedirect(result.ReturnUrl);
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
        public IActionResult Metadata() => saml.CreateMetadata();
    }
}
