namespace BuergerPortal.Web.Features.Settings.Contracts
{
    public sealed class UserSettingsUpdateRequest
    {
        public string Theme { get; set; } = "Dark";   // "Light" | "Dark"
        public string Language { get; set; } = "de";
        public bool PushEnabled { get; set; }
        public bool ReduceDataUsage { get; set; }
        public bool AnalyticsOptIn { get; set; }
        public bool AllowGeolocation { get; set; }
        public string? Version { get; set; }
    }
}
