using BuergerPortal.Web.Features.Termine.ViewModels;

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
    }

}
