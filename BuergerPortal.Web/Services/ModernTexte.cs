using Microsoft.AspNetCore.Mvc.Localization;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Texte, die es nur in Portal 2 gibt (<c>Resources/Services/ModernTexte(.en).resx</c>), in Views über
/// <c>@inject IHtmlLocalizer&lt;ModernTexte&gt; M</c>. Texte, die beide Oberflächen zeigen, bleiben in den Ressourcen der
/// klassischen Views und werden mit <see cref="KlassischeTexte.Fuer"/> geholt – so wird nichts doppelt übersetzt.
/// </summary>
public sealed class ModernTexte
{
}

public static class KlassischeTexte
{
    private static readonly string Anwendung = typeof(ModernTexte).Assembly.GetName().Name!;

    /// <summary>
    /// Localizer mit den Texten einer klassischen View, z. B. <c>"Shared/_TabLeiste"</c> →
    /// <c>Resources/Views/Shared/_TabLeiste(.en).resx</c> – derselbe Name, den <see cref="IViewLocalizer"/> für die View
    /// bildet.
    /// </summary>
    public static IHtmlLocalizer Fuer(this IHtmlLocalizerFactory factory, string view) =>
        factory.Create(Anwendung + ".Views." + view.Replace('/', '.'), Anwendung);
}
