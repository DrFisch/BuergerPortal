namespace BuergerPortal.Web.Features.Antraege
{
    /// <summary>
    /// Zeitleiste eines Antrags (2026-10-18, gemeinsam für alle Antragsarten): angelegt → eingereicht → in Bearbeitung →
    /// abgeschlossen. Zeitpunkte in deutscher Zeit aus dem Statusverlauf der API; ältere Anträge haben keinen Verlauf.
    /// </summary>
    public sealed record AntragVerlaufVm(int Status, DateTime Angelegt, DateTime? Eingereicht, DateTime? InBearbeitung,
        DateTime? Abgeschlossen)
    {
        public bool IstEingereicht => Status >= 2;
        public bool IstInBearbeitung => Status >= 3;
        public bool IstAbgeschlossen => Status >= 4;

        /// <summary>Hinweis auf die simulierte Bearbeitung, solange der Antrag eingereicht, aber nicht abgeschlossen ist.</summary>
        public bool ZeigeSimulationsHinweis => IstEingereicht && !IstAbgeschlossen;
    }
}
