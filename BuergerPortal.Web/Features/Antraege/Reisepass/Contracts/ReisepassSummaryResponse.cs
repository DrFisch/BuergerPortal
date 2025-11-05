namespace BuergerPortal.Web.Features.Antraege.Reisepass.Contracts
{
    public sealed class ReisepassSummaryResponse
    {
        public Guid Id { get; set; }
        public string Vorname { get; set; } = "";
        public string Nachname { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }
        public int Status { get; set; }           
    }
}
