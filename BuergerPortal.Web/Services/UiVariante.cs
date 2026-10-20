using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Schalter für die Oberfläche (Abschnitt <c>Ui</c> in den appsettings, E-027/E-028).
/// <c>Modern=true</c> zeigt Portal 2, <c>false</c> die bisherige Oberfläche – Standard für alle Besucher.
/// <c>UrlUmschalter=true</c> erlaubt zusätzlich <c>?ui=modern|klassisch|standard</c> für den eigenen Browser.
/// Im Betrieb überschreibt die Umgebungsvariable <c>Ui__Modern</c> (aus der .env, <c>deploy/ui-modus.ps1</c>) den Wert.
/// </summary>
public sealed class UiOptions
{
    public const string Section = "Ui";

    public bool Modern { get; set; }

    public bool UrlUmschalter { get; set; } = true;
}

/// <summary>
/// Die eine Stelle, die je Anfrage entscheidet, welche Oberfläche kommt: zuerst die eigene Wahl des Browsers (Cookie
/// <see cref="CookieName"/>, nur wenn der URL-Umschalter an ist), sonst <see cref="UiOptions.Modern"/>. View-Suche,
/// Layout und Tests lesen nur das Ergebnis aus <see cref="IsModern(HttpContext)"/>.
/// </summary>
public static class UiVariante
{
    public const string Modern = "modern";
    public const string Klassisch = "klassisch";
    public const string Standard = "standard";
    public const string QueryKey = "ui";

    /// <summary>Sitzungs-Cookie mit Präfix __Host- (Secure, Path=/, ohne Domain): gilt nur für das Portal selbst.</summary>
    public const string CookieName = "__Host-bp-ui";

    private const string ItemKey = "bpsim.ui.modern";

    /// <summary>Entscheidung aus Einstellung und Cookie; unbekannte Cookie-Werte zählen nicht.</summary>
    public static bool Decide(UiOptions options, string? cookieValue)
    {
        if (options.UrlUmschalter)
        {
            if (cookieValue == Modern) return true;
            if (cookieValue == Klassisch) return false;
        }
        return options.Modern;
    }

    /// <summary>Ergebnis der Entscheidung für diese Anfrage (ohne Middleware, z. B. in Tests: klassisch).</summary>
    public static bool IsModern(HttpContext context) => context.Items[ItemKey] is true;

    /// <summary>Legt die Entscheidung für diese Anfrage fest (Middleware; in Tests direkt).</summary>
    public static void SetModern(HttpContext context, bool modern) => context.Items[ItemKey] = modern;

    /// <summary>
    /// Wertet <c>?ui=</c> aus (nur GET, nur bei eingeschaltetem URL-Umschalter): <c>modern</c>/<c>klassisch</c> merkt die
    /// Wahl im Cookie, <c>standard</c> löscht sie; danach Weiterleitung auf dieselbe Adresse ohne <c>ui</c>. Andere Werte
    /// werden ignoriert. Anschließend wird für jede Anfrage entschieden und das Ergebnis in <c>HttpContext.Items</c>
    /// gelegt. Die Einstellung wird bei jeder Anfrage neu gelesen – eine geänderte appsettings-Datei wirkt ohne Neustart.
    /// </summary>
    public static IApplicationBuilder UseUiVariante(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var options = context.RequestServices.GetRequiredService<IOptionsMonitor<UiOptions>>().CurrentValue;
        var request = context.Request;

        if (options.UrlUmschalter && HttpMethods.IsGet(request.Method) && request.Query.TryGetValue(QueryKey, out var wahl))
        {
            var wert = wahl.ToString();
            if (wert is Modern or Klassisch or Standard)
            {
                if (wert == Standard)
                {
                    context.Response.Cookies.Delete(CookieName, UiCookieOptions());
                }
                else
                {
                    context.Response.Cookies.Append(CookieName, wert, UiCookieOptions());
                }
                context.Response.Redirect(AdresseOhneUi(request));
                return;
            }
        }

        SetModern(context, Decide(options, request.Cookies[CookieName]));
        await next();
    });

    /// <summary>Sitzungs-Cookie (kein Ablaufdatum): Die Wahl endet mit dem Browser.</summary>
    private static CookieOptions UiCookieOptions() => new()
    {
        Secure = true,
        HttpOnly = true,
        Path = "/",
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
    };

    /// <summary>
    /// Dieselbe Adresse ohne den Parameter <c>ui</c>. Beginnt der Pfad mit "//", würde der Browser ihn als fremden Host
    /// lesen (offene Weiterleitung) – dann geht es zur Startseite. Ein "\" kommt maskiert (%5C) an und ist harmlos.
    /// </summary>
    public static string AdresseOhneUi(HttpRequest request)
    {
        var pfad = (request.PathBase + request.Path).ToString();
        if (pfad.Length == 0 || pfad.StartsWith("//", StringComparison.Ordinal))
        {
            pfad = "/";
        }
        var rest = request.Query
            .Where(p => !string.Equals(p.Key, QueryKey, StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => p.Value.Select(v => new KeyValuePair<string, string>(p.Key, v ?? "")));
        return pfad + new QueryBuilder(rest).ToQueryString();
    }
}
