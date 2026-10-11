using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Domain.Antrag.Entity
{
    /// <summary>
    /// Ein Statuswechsel eines Antrags (eingereicht, in Bearbeitung, genehmigt …) mit Zeitpunkt – für die Zeitleiste im
    /// Portal und die Statusmeldungen ins BundID-Postfach (Vorbild: ZBP CreateState).
    /// </summary>
    public sealed class AntragStatusEreignis
    {
        public Guid Id { get; set; }
        public Guid AntragId { get; set; }
        public AntragStatus Status { get; set; }
        public DateTime ZeitpunktUtc { get; set; }
    }
}
