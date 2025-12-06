namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts
{
    public sealed class SperrmuellStep1Request
    {
        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
        public DateOnly Geburtsdatum { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
    }
}
