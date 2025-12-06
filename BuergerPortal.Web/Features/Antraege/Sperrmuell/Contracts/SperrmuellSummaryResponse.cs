namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts
{
    public class SperrmuellSummaryResponse
    {
        public Guid Id { get; set; }
        public string Vorname { get; set; } = "";
        public string Nachname { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        public int Status { get; set; }
        public int Typ { get; set; }
    }
}
