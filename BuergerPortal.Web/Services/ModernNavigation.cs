using Microsoft.AspNetCore.Mvc.Rendering;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Navigation von Portal 2 (Leiste links, Tab-Leiste unten, Konto-Blatt): welcher Bereich gerade aktiv ist – für
/// <c>aria-current="page"</c> – und die Initialen für den Avatar.
/// </summary>
public static class ModernNavigation
{
    public const string Start = "start";
    public const string Termine = "termine";
    public const string Antraege = "antraege";
    public const string MeineDaten = "meinedaten";
    public const string MangelMelden = "mangelmelden";
    public const string Meldungen = "meldungen";
    public const string Entdecken = "entdecken";
    public const string Einstellungen = "einstellungen";

    /// <summary>Bereich der aktuellen Seite aus Controller und Aktion; null, wenn keiner passt.</summary>
    public static string? Bereich(ViewContext context)
    {
        var controller = context.RouteData.Values["controller"] as string ?? "";
        var action = context.RouteData.Values["action"] as string ?? "";
        bool Ist(string name) => string.Equals(controller, name, StringComparison.OrdinalIgnoreCase);
        return controller switch
        {
            _ when Ist("Home") && string.Equals(action, "Index", StringComparison.OrdinalIgnoreCase) => Start,
            _ when Ist("Termine") => Termine,
            _ when Ist("Antraege") || Ist("ReisepassAntraege") || Ist("SperrmuellAntraege") || Ist("MeldebescheinigungAntraege") => Antraege,
            _ when Ist("MeineDaten") => MeineDaten,
            _ when Ist("Maengel") && string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase) => MangelMelden,
            _ when Ist("Maengel") => Meldungen,
            _ when Ist("Entdecken") => Entdecken,
            _ when Ist("Settings") => Einstellungen,
            _ => null,
        };
    }

    /// <summary>"page", wenn die Seite zu einem der Bereiche gehört (für aria-current), sonst null (Attribut entfällt).</summary>
    public static string? Aktuell(ViewContext context, params string[] bereiche) =>
        Bereich(context) is { } b && bereiche.Contains(b) ? "page" : null;

    /// <summary>Initialen aus Vor- und Familienname (BundID), sonst aus dem Anzeigenamen; höchstens zwei Buchstaben.</summary>
    public static string Initialen(BundIdUser? user)
    {
        if (user is null) return "?";
        if (!string.IsNullOrWhiteSpace(user.GivenName) && !string.IsNullOrWhiteSpace(user.FamilyName))
        {
            return string.Concat(char.ToUpperInvariant(user.GivenName.Trim()[0]), char.ToUpperInvariant(user.FamilyName.Trim()[0]));
        }
        var teile = user.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return teile.Length switch
        {
            0 => "?",
            1 => char.ToUpperInvariant(teile[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(teile[0][0]), char.ToUpperInvariant(teile[^1][0])),
        };
    }
}
