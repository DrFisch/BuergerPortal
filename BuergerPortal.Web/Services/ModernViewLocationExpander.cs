using Microsoft.AspNetCore.Mvc.Razor;

namespace BuergerPortal.Web.Services;

/// <summary>
/// View-Suche für Portal 2: Ist für die Anfrage die moderne Oberfläche gewählt (<see cref="UiVariante.IsModern"/>),
/// sucht Razor zuerst unter <c>Views/Modern/{Controller}/</c> und <c>Views/Modern/Shared/</c>, danach wie bisher.
/// Fehlt eine moderne View, kommt die klassische – so entsteht Portal 2 Seite für Seite, ohne kaputte Seite. Die
/// klassische Oberfläche sucht nie unter <c>Views/Modern/</c>.
/// </summary>
public sealed class ModernViewLocationExpander : IViewLocationExpander
{
    private const string ValueKey = "ui";

    /// <summary>Der Wert gehört zum Cache-Schlüssel der View-Suche – modern und klassisch werden getrennt gemerkt.</summary>
    public void PopulateValues(ViewLocationExpanderContext context) =>
        context.Values[ValueKey] = UiVariante.IsModern(context.ActionContext.HttpContext) ? UiVariante.Modern : UiVariante.Klassisch;

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue(ValueKey, out var ui) || ui != UiVariante.Modern)
        {
            return viewLocations;
        }
        return new[] { "/Views/Modern/{1}/{0}.cshtml", "/Views/Modern/Shared/{0}.cshtml" }.Concat(viewLocations);
    }
}
