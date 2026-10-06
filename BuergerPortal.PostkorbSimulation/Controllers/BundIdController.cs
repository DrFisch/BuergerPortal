using BuergerPortal.BundId;
using BuergerPortal.PostkorbSimulation.Auth;
using BuergerPortal.PostkorbSimulation.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.PostkorbSimulation.Controllers;

/// <summary>
/// Anmeldung am Postfach über die BundID. Das Postfach ist ein eigener SAML-Service-Provider (eigene EntityID,
/// eigener ACS) – wie bei der echten BundID, die das Postfach erst nach dem Login zeigt. Den SAML-Ablauf liefert
/// BundIdSamlService (dieselben Prüfungen wie beim Auth-Server).
/// </summary>
[AllowAnonymous]
[Route("bundid")]
public class BundIdController(BundIdSamlService saml, ILogger<BundIdController> logger) : Controller
{
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

    // Assertion Consumer Service: SAML-Response der BundID (HTTP-POST vom Browser).
    [HttpPost("acs")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AssertionConsumerService(CancellationToken ct)
    {
        var result = await saml.ProcessResponseAsync(Request, Response, ct);
        switch (result.Outcome)
        {
            case BundIdLoginOutcome.Cancelled:
                return LocalRedirect("~/?anmeldung=abgebrochen");
            case BundIdLoginOutcome.Failed:
                return LoginError(result.ErrorTitle, result.ErrorMessage, result.RequestedLevel, result.ReturnUrl);
        }

        // Ohne Postkorb-Handle gibt es kein Postfach, das angezeigt werden könnte.
        var attributes = result.Attributes!;
        if (!Guid.TryParse(attributes.PostkorbHandle, out var handle) || handle == Guid.Empty)
        {
            logger.LogWarning("BundID-Anmeldung ohne verwendbares Postkorb-Handle");
            return LoginError("Kein Postfach gefunden",
                "Die BundID hat für dieses Konto kein Postkorb-Handle übermittelt. Ohne Postkorb-Handle kann kein " +
                "Postfach angezeigt werden.", result.RequestedLevel, result.ReturnUrl);
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            PostfachSession.Create(attributes.DisplayName, handle, attributes.TrustLevel));
        logger.LogInformation("Postfach-Anmeldung über die BundID, Niveau {Level}", attributes.TrustLevel);
        return LocalRedirect(result.ReturnUrl);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("~/?anmeldung=abgemeldet");
    }

    // SP-Metadaten des Postfachs (eigene EntityID, eigener ACS).
    [HttpGet("metadata")]
    public IActionResult Metadata() => saml.CreateMetadata();

    private ViewResult LoginError(string title, string message, int retryLevel, string returnUrl) =>
        View("Error", new LoginErrorViewModel
        {
            Title = title,
            Message = message,
            RetryUrl = Url.Action(nameof(Login), new { level = retryLevel, returnUrl }) ?? Url.Content("~/bundid/login"),
        });
}
