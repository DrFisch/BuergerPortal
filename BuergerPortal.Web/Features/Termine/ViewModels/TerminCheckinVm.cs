namespace BuergerPortal.Web.Features.Termine.ViewModels
{
    public enum CheckinStatus
    {
        Gueltig,
        Storniert,
        Vorbei,
        Ungueltig,      // Kennung falsch, verändert oder abgelaufen, Termin unbekannt
    }

    /// <summary>Seite nach dem Scannen des QR-Codes im Bürgeramt – bewusst ohne Personendaten.</summary>
    public sealed class TerminCheckinVm
    {
        public CheckinStatus Status { get; init; }
        public ServiceType? Service { get; init; }
        public LocationType? Location { get; init; }
        public DateTime? Start { get; init; }
        public DateTime? End { get; init; }
        public bool Heute { get; init; }
    }
}
