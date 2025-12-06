namespace BuergerPortal.Web.Features.Antraege.Reisepass.ViewModels
{
    public sealed class AntragDetailVm
    {
        public Guid Id { get; set; }
        public string Antragsteller { get; set; } = "";
        public DateTime Geburtsdatum { get; set; }
        public string StatusText { get; set; } = "";
        public string BadgeClass { get; set; } = "text-bg-secondary";
        public int ProgressPercent { get; set; }

        public bool Express { get; set; }
        public bool AltpassVorhanden { get; set; }
        public string? Hinweis { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
    }
}
