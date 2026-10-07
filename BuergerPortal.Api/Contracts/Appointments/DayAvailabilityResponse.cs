namespace BuergerPortal.Api.Contracts.Appointments
{
    /// <summary>
    /// Verfügbarkeit eines Tages an einem Standort für die Buchungsseite. Zeiten in Ortszeit (Europe/Berlin) als "HH:mm".
    /// </summary>
    public sealed class DayAvailabilityResponse
    {
        public DateOnly Date { get; init; }

        /// <summary>Grund, warum an diesem Tag nichts buchbar ist (Wochenende, Feiertag, Vorlauf), sonst null.</summary>
        public string? ClosedReason { get; init; }

        /// <summary>Geschäftszeit der Bürgerämter.</summary>
        public string OpensAt { get; init; } = "08:00";
        public string ClosesAt { get; init; } = "12:00";

        /// <summary>Am gewählten Standort bereits vergebene Zeiten (ohne Angaben zu anderen Personen).</summary>
        public List<TimeRangeResponse> Taken { get; init; } = [];

        /// <summary>Eigene Termine der angemeldeten Person an diesem Tag (alle Standorte).</summary>
        public List<TimeRangeResponse> Own { get; init; } = [];
    }

    public sealed record TimeRangeResponse(string Start, string End);
}
