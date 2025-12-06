namespace BuergerPortal.Web.Features.Antraege.Reisepass.Contracts
{
    public sealed class ReisepassStep1Request
    {
        public string Vorname { get; set; } = "";
        public string Nachname { get; set; } = "";
        public DateOnly Geburtsdatum { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
    }
}
