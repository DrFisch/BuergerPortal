using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Regeln der Anmeldesitzung im Portal. Bei jedem Aufruf mit Sitzungscookie prüft <see cref="ValidateAsync"/>, ob die
/// Sitzung noch gilt – vorher fiel ein abgelaufenes Access-Token erst beim nächsten API-Aufruf auf (die Seite wurde
/// noch angemeldet angezeigt, erst der folgende Klick meldete ab).
/// </summary>
public static class PortalSession
{
    /// <summary>So lange vor Ablauf wird das Access-Token erneuert.</summary>
    public static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);

    /// <summary>Markierung im HttpContext: Die Sitzung wurde in diesem Aufruf wegen Ablaufs beendet.</summary>
    public const string ExpiredItem = "bpsim.sitzung.abgelaufen";

    public enum Outcome { Valid, Refresh, Expired }

    /// <summary>Reine Entscheidung (testbar): Token noch lange gültig, bald zu erneuern oder abgelaufen.</summary>
    public static Outcome Evaluate(DateTimeOffset now, DateTimeOffset? tokenExpiresAt)
    {
        if (tokenExpiresAt is null || tokenExpiresAt - now > RefreshBefore)
        {
            return Outcome.Valid;
        }
        return tokenExpiresAt > now ? Outcome.Refresh : Outcome.Expired;
    }

    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var now = DateTimeOffset.UtcNow;
        var outcome = Evaluate(now, AccessTokenRefresher.ExpiresAt(ctx.Properties));
        if (outcome == Outcome.Valid)
        {
            return;
        }

        var refresher = ctx.HttpContext.RequestServices.GetRequiredService<AccessTokenRefresher>();
        if (await refresher.TryRefreshAsync(ctx.Properties, ctx.HttpContext.RequestAborted))
        {
            ctx.ShouldRenew = true; // neue Tokens ins Cookie schreiben
            return;
        }

        // Kurz vor Ablauf darf das alte Token noch genutzt werden; danach endet die Sitzung sofort, damit die Seite
        // nicht mehr als angemeldet erscheint.
        if (outcome == Outcome.Expired)
        {
            await EndAsync(ctx);
        }
    }

    private static async Task EndAsync(CookieValidatePrincipalContext ctx)
    {
        ctx.RejectPrincipal();
        await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.HttpContext.Items[ExpiredItem] = true;
    }
}
