namespace BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels
{
    public sealed class StatusListItemVm
    {
        public Guid Id { get; set; }
        public string Antragsteller { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string BadgeClass { get; set; } = "text-bg-secondary";
        public int ProgressPercent { get; set; }
        public DateTime Angelegt { get; set; }
        public DateTime? Eingereicht { get; set; }
    }

    public sealed class StatusListeVm
    {
        public List<StatusListItemVm> Items { get; set; } = new();
    }
}