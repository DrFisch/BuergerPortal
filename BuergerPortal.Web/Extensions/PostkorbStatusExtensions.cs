using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BuergerPortal.Web.Extensions
{
    /// <summary>
    /// Die API meldet im Antwort-Header "X-Postkorb-Status", ob die Bestätigung im BundID-Postfach angekommen ist
    /// (zugestellt / kein-postfach / fehlgeschlagen). Der Vorgang selbst ist in jedem Fall gespeichert.
    /// </summary>
    public static class PostkorbStatusExtensions
    {
        public const string HeaderName = "X-Postkorb-Status";
        public const string TempDataKey = "PostkorbHinweis";

        /// <summary>Merkt für die nächste Seite einen Hinweis, wenn die Zustellung fehlgeschlagen ist.</summary>
        public static void MerkePostkorbStatus(this ITempDataDictionary tempData, HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues(HeaderName, out var values) && values.Contains("fehlgeschlagen"))
            {
                tempData[TempDataKey] = "fehlgeschlagen";
            }
        }
    }
}
