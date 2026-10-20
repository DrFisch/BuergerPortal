using BuergerPortal.Web.Features.Antraege;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Symbole und Farbton von Portal 2 je Leistung des Antragskatalogs (Features/Antraege/Leistungen.cs) – die bisherige
/// Oberfläche nutzt dort Bootstrap-Icons (LeistungInfo.Icon), Portal 2 den eigenen Satz (wwwroot/modern/symbole.svg).
/// </summary>
public static class ModernSymbole
{
    public static string Leistung(LeistungInfo? leistung) => leistung?.Typ switch
    {
        Leistungen.Reisepass => "passport",
        Leistungen.Sperrmuell => "truck",
        Leistungen.Meldebescheinigung => "home-doc",
        _ => "file-text",
    };

    /// <summary>Klasse der Symbol-Kachel (bausteine.css) aus der Farbe des Katalogs.</summary>
    public static string KachelTon(LeistungInfo? leistung) => leistung?.Farbe switch
    {
        "success" => "m-kachel--erfolg",
        "info" => "m-kachel--hinweis",
        "warning" => "m-kachel--warnung",
        _ => "",
    };

    /// <summary>
    /// Wetter-Symbol aus der Bootstrap-Icon-Klasse, die HomeController.Index liefert (WeatherVm.IconClass). Dasselbe
    /// Kürzel benennt in Portal 2 auch den Text (ModernTexte „Wetter_…“) – die Beschreibung aus dem Controller ist im
    /// Quelltext falsch kodiert (das „ö“ in „bewölkt“ kommt kaputt an) und nur deutsch.
    /// </summary>
    public static string Wetter(string? iconClass) => iconClass switch
    {
        "bi-sun" => "sun",
        "bi-cloud-sun" => "cloud-sun",
        "bi-cloud-fog" => "cloud-fog",
        "bi-cloud-rain" => "cloud-rain",
        "bi-cloud-snow" => "cloud-snow",
        "bi-cloud-lightning" => "cloud-lightning",
        _ => "cloud",
    };
}
