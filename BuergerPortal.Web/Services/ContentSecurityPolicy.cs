using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Content-Security-Policy des Portals. Skripte laufen nur, wenn sie vom eigenen Host (bzw. Leaflet von unpkg.com)
/// stammen oder als Inline-Skript die <b>Nonce</b> dieser Antwort tragen – eine Zufallszahl, die bei jeder Anfrage neu
/// entsteht. Ein eingeschleustes Skript kennt sie nicht und wird nicht ausgeführt. Die Nonce setzt
/// <see cref="ScriptNonceTagHelper"/> an jedes &lt;script&gt; der Razor-Ansichten.
/// Header-Name aus <c>Csp:Header</c>: <c>Content-Security-Policy-Report-Only</c> (Standard: Verstöße nur in der
/// Browser-Konsole melden) oder <c>Content-Security-Policy</c> (durchsetzen). Die übrigen Hosts (Auth-Server,
/// Simulator) bekommen ihre Regeln von Caddy (deploy/Caddyfile).
/// </summary>
public static class ContentSecurityPolicy
{
    public const string ReportOnlyHeader = "Content-Security-Policy-Report-Only";
    public const string EnforceHeader = "Content-Security-Policy";
    public const string ConfigKey = "Csp:Header";
    private const string NonceKey = "bpsim.csp.nonce";

    /// <summary>Header-Name laut Konfiguration; unbekannte Werte gelten als "nur melden".</summary>
    public static string HeaderName(IConfiguration configuration) =>
        string.Equals(configuration[ConfigKey], EnforceHeader, StringComparison.OrdinalIgnoreCase) ? EnforceHeader : ReportOnlyHeader;

    /// <summary>
    /// Die Regeln. <paramref name="authorityOrigin"/> = Adresse des Auth-Servers (Abmelden leitet dorthin weiter).
    /// Karten: Leaflet von unpkg.com, Kacheln von OpenStreetMap/CARTO, Adresssuche über Nominatim.
    /// Styles dürfen inline sein (viele style-Attribute in den Ansichten) – das Risiko ist deutlich kleiner als bei Skripten.
    /// </summary>
    public static string Build(string nonce, string? authorityOrigin) => string.Join("; ",
        "default-src 'self'",
        $"script-src 'self' 'nonce-{nonce}' https://unpkg.com",
        "style-src 'self' 'unsafe-inline' https://unpkg.com",
        "img-src 'self' data: blob: https://*.tile.openstreetmap.org https://*.basemaps.cartocdn.com https://unpkg.com",
        "connect-src 'self' https://nominatim.openstreetmap.org",
        "font-src 'self'",
        "manifest-src 'self'",
        "worker-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'" + (string.IsNullOrEmpty(authorityOrigin) ? "" : " " + authorityOrigin),
        "frame-ancestors 'none'");

    public static string? GetNonce(HttpContext context) => context.Items[NonceKey] as string;

    /// <summary>Erzeugt je Anfrage eine Nonce und setzt den Header (auch für statische Dateien und Fehlerseiten).</summary>
    public static IApplicationBuilder UseContentSecurityPolicy(this WebApplication app)
    {
        var header = HeaderName(app.Configuration);
        var authorityOrigin = Uri.TryCreate(app.Configuration["Authentication:Authority"], UriKind.Absolute, out var authority)
            ? authority.GetLeftPart(UriPartial.Authority)
            : null;

        return app.Use(async (context, next) =>
        {
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[header] = Build(nonce, authorityOrigin);
                return Task.CompletedTask;
            });
            await next();
        });
    }
}

/// <summary>Setzt die Nonce der aktuellen Antwort an jedes &lt;script&gt;-Element der Razor-Ansichten.</summary>
[HtmlTargetElement("script")]
public sealed class ScriptNonceTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var nonce = ContentSecurityPolicy.GetNonce(ViewContext.HttpContext);
        if (nonce != null && !output.Attributes.ContainsName("nonce"))
        {
            output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}
