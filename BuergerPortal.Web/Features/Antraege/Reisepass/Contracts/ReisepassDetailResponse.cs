namespace BuergerPortal.Web.Features.Antraege.Reisepass.Contracts
{
    public sealed class ReisepassDetailResponse
    {
        public Guid Id { get; set; }
        public string Vorname { get; set; } = "";
        public string Nachname { get; set; } = "";
        public DateOnly Geburtsdatum { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public bool? Express { get; set; }
        public bool? AltpassVorhanden { get; set; }
        public int Status { get; set; } 
    }
}
