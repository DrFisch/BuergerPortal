namespace BuergerPortal.Api.Contracts.Settings
{
    public sealed class ThemeUpdateRequest
    {
        public string Theme { get; set; } = "light"; // "light" | "dark" | evtl. "system"
    }
}
