using BuergerPortal.Web.Features.Termine.ViewModels;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BuergerPortal.Web.Extensions
{
    public static class EnumDisplayExtensions
    {
        public static string ToDisplay(this LocationType v) => v switch
        {
            LocationType.BuergermtMitte => "Bürgeramt Mitte",
            LocationType.BuergermtNord => "Bürgeramt Nord",
            LocationType.BuergermtSued => "Bürgeramt Süd",
            _ => v.ToString()
        };

        public static string ToDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttribute<DisplayAttribute>();
            return attr?.GetName() ?? value.ToString();
        }

        // Übersetzter Anzeigename (Plan A9): Schlüssel ist der deutsche Display-Name, Englisch in
        // Resources/Features/Termine/ViewModels/<Enum>.en.resx – dieselben Dateien nutzt Html.GetEnumSelectList.
        public static string ToDisplayName(this Enum value, IStringLocalizerFactory localizerFactory)
            => localizerFactory.Create(value.GetType())[value.ToDisplayName()].Value;
    }

}
