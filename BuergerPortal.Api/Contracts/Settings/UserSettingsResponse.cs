namespace BuergerPortal.Api.Contracts.Settings
{
    public sealed class UserSettingsResponse
    {
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "de";
        public bool PushEnabled { get; set; }
        public bool ReduceDataUsage { get; set; }
        public bool AnalyticsOptIn { get; set; }
        public bool AllowGeolocation { get; set; }
        public string Version { get; set; } = ""; // Base64 RowVersion
    }

}
