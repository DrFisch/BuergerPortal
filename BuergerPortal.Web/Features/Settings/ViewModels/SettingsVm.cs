using Microsoft.AspNetCore.Mvc.Rendering;

namespace BuergerPortal.Web.Features.Settings.ViewModels
{
    public sealed class SettingsVm
    {
        // Nur "Light" oder "Dark"
        public string Theme { get; set; } = "Light";

        // "de" | "en" (oder weitere)
        public string Language { get; set; } = "de";
        public IEnumerable<SelectListItem> Languages { get; set; } = Enumerable.Empty<SelectListItem>();

        public bool PushEnabled { get; set; }
        public bool ReduceDataUsage { get; set; }
        public bool AnalyticsOptIn { get; set; }
        public bool AllowGeolocation { get; set; }
    }
}
