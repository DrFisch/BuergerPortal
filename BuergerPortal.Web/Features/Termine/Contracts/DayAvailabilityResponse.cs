namespace BuergerPortal.Web.Features.Termine.Contracts
{
    /// <summary>Verfügbarkeit eines Tages an einem Standort (API GET api/appointments/availability), Zeiten "HH:mm".</summary>
    public sealed class DayAvailabilityResponse
    {
        public DateOnly Date { get; set; }
        public string? ClosedReason { get; set; }
        public string OpensAt { get; set; } = "08:00";
        public string ClosesAt { get; set; } = "12:00";
        public List<TimeRangeResponse> Taken { get; set; } = [];
        public List<TimeRangeResponse> Own { get; set; } = [];
    }

    public sealed record TimeRangeResponse(string Start, string End);
}
