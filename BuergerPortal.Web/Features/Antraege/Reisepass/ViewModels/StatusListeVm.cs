namespace BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels
{
    public sealed class StatusListItemVm
    {
        public Guid Id { get; set; }

        public string Antragsteller { get; set; } = "";

        // --- Typ (aus API) ---
        public int Typ { get; set; }
        public string TypText { get; set; } = "";
        public string TypBadgeClass { get; set; } = "";

        // --- Status ---
        public string StatusText { get; set; } = "";
        public string BadgeClass { get; set; } = "text-bg-secondary";
        public int ProgressPercent { get; set; }

        // --- Zeitpunkte ---
        public DateTime Angelegt { get; set; }
        public DateTime? Eingereicht { get; set; }
    }

    public sealed class StatusListeVm
    {
        public List<StatusListItemVm> Items { get; set; } = new();
    }
}