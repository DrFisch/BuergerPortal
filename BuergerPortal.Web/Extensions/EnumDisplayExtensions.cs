using BuergerPortal.Web.Features.Termine.ViewModels;
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
    }

}
