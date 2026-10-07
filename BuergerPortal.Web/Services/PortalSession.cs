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
    /// <summary>
    /// Ohne Aktivität endet die Sitzung nach dieser Zeit (jeder Seitenaufruf verlängert sie). Standard 30 Minuten,
    /// zum Testen kürzer einstellbar (Sitzung:InaktivitaetMinuten, siehe <see cref="Configure"/>).
    /// </summary>
    public static TimeSpan IdleTimeout { get; private set; } = TimeSpan.FromMinutes(30);

    /// <summary>Spätestens nach dieser Zeit ist eine neue BundID-Anmeldung nötig, auch bei laufender Nutzung.</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(8);

    /// <summary>Zeitpunkt der BundID-Anmeldung in den Sitzungsdaten (AuthenticationProperties.Items).</summary>
    public const string LoginTimeKey = ".bpsim.anmeldung";

    /// <summary>Abfrage der Restzeit (sitzung.js) – zählt nicht als Aktivität.</summary>
    public const string StatusPath = "/Auth/Sitzung";

    /// <summary>So lange vor Ablauf wird das Access-Token erneuert.</summary>
    public static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);

    /// <summary>Markierung im HttpContext: Die Sitzung wurde in diesem Aufruf wegen Ablaufs beendet.</summary>
    public const string ExpiredItem = "bpsim.sitzung.abgelaufen";

    public enum Outcome { Valid, Refresh, Expired }

    /// <summary>Beim Start einmal aufrufen (Program.cs).</summary>
    public static void Configure(IConfiguration configuration) =>
        IdleTimeout = TimeSpan.FromMinutes(configuration.GetValue("Sitzung:InaktivitaetMinuten", 30));

    /// <summary>
    /// Reine Entscheidung (testbar): Höchstdauer seit der Anmeldung überschritten → abgelaufen; sonst nach dem
    /// Access-Token: noch lange gültig, bald zu erneuern oder abgelaufen.
    /// </summary>
    public static Outcome Evaluate(DateTimeOffset now, DateTimeOffset? loginUtc, DateTimeOffset? tokenExpiresAt)
    {
        if (loginUtc is not null && now - loginUtc >= MaxLifetime)
        {
            return Outcome.Expired;
        }
        if (tokenExpiresAt is null || tokenExpiresAt - now > RefreshBefore)
        {
            return Outcome.Valid;
        }
        return tokenExpiresAt > now ? Outcome.Refresh : Outcome.Expired;
    }

    /// <summary>Restzeit bis zum automatischen Abmelden direkt nach einem Seitenaufruf.</summary>
    public static TimeSpan Remaining(DateTimeOffset now, DateTimeOffset? loginUtc)
    {
        var untilMax = loginUtc is null ? IdleTimeout : loginUtc.Value + MaxLifetime - now;
        return untilMax < IdleTimeout ? (untilMax > TimeSpan.Zero ? untilMax : TimeSpan.Zero) : IdleTimeout;
    }

    public static DateTimeOffset? LoginTime(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(LoginTimeKey, out var value) &&
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var login) ? login : null;

    /// <summary>Nach der BundID-Anmeldung (OnTokenValidated): Anmeldezeit merken, Ablauf nach Inaktivität setzen.</summary>
    public static void Start(AuthenticationProperties properties, DateTimeOffset now)
    {
        properties.IsPersistent = false;
        properties.IssuedUtc = now;
        properties.ExpiresUtc = now + IdleTimeout;
        properties.Items[LoginTimeKey] = now.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var now = DateTimeOffset.UtcNow;
        var login = LoginTime(ctx.Properties);
        var outcome = Evaluate(now, login, AccessTokenRefresher.ExpiresAt(ctx.Properties));
        var maxReached = login is not null && now - login >= MaxLifetime;
        // Die Restzeit-Abfrage (sitzung.js) zählt nicht als Aktivität: nichts verlängern, nur ein Ende feststellen.
        var activity = !ctx.HttpContext.Request.Path.Equals(StatusPath, StringComparison.OrdinalIgnoreCase);

        // Token bald oder schon abgelaufen (Höchstdauer nicht erreicht): mit dem Refresh-Token erneuern
        if (activity && outcome != Outcome.Valid && !maxReached)
        {
            var refresher = ctx.HttpContext.RequestServices.GetRequiredService<AccessTokenRefresher>();
            if (await refresher.TryRefreshAsync(ctx.Properties, ctx.HttpContext.RequestAborted))
            {
                outcome = Outcome.Valid;
            }
        }

        // Abgelaufen (Höchstdauer erreicht oder Token nicht erneuerbar): Sitzung sofort beenden, damit die Seite
        // nicht mehr als angemeldet erscheint. Kurz vor Ablauf darf das alte Token noch genutzt werden.
        if (outcome == Outcome.Expired)
        {
            await EndAsync(ctx);
            return;
        }

        // Jeder Aufruf zählt als Aktivität: Cookie neu ausstellen (mit ggf. neuen Tokens), Ablauf wieder IdleTimeout
        // ab jetzt. Die eingebaute SlidingExpiration verlängert erst nach der halben Laufzeit (Grenze 15 bis 30 min).
        if (activity)
        {
            ctx.ShouldRenew = true;
        }
    }

    /// <summary>Name des Sitzungscookies (Standard des Cookie-Schemas, bei Größe in Teile C1, C2 … aufgeteilt).</summary>
    public const string CookieName = ".AspNetCore.Cookies";

    /// <summary>
    /// Ist die Sitzung abgelaufen (statt nie begonnen oder abgemeldet)? Entweder hat dieser Aufruf sie beendet, oder der
    /// Browser schickt noch ein Sitzungscookie, das nicht mehr gilt (Inaktivität: Ablaufzeit im Cookie überschritten).
    /// </summary>
    public static bool WasExpired(HttpContext http) =>
        http.Items.ContainsKey(ExpiredItem) ||
        (http.User.Identity?.IsAuthenticated != true && http.Request.Cookies.ContainsKey(CookieName));

    private static async Task EndAsync(CookieValidatePrincipalContext ctx)
    {
        ctx.RejectPrincipal();
        await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.HttpContext.Items[ExpiredItem] = true;
    }
}
