using BuergerPortal.Api.Postkorb;
using BuergerPortal.Api.Push;
using BuergerPortal.Application.Antraege.Status;
using BuergerPortal.Application.Interfaces.Postkorb;
using BuergerPortal.Domain.Antrag.Enums;

namespace BuergerPortal.Api.Antraege
{
    /// <summary>
    /// Statusmeldung zu einem Antrag für das BundID-Postfach und die Benachrichtigung aufs Handy. Aufbau nach dem Vorbild
    /// von ZBP „CreateState“ (applicationId → Vorgangsnummer, state → Status, createdDate → Zeitpunkt, senderName →
    /// Behörde, publicServiceName → Leistung, additionalInformation → Hinweis). Zugestellt wird sie hier als Nachricht
    /// (CreateMessage) – einen eigenen Status-Kanal hat die Postfach-Simulation nicht.
    /// </summary>
    public static class AntragStatusmeldung
    {
        public const string Absender = "BürgerPortal (Studienprojekt)";

        public static PostkorbMessage Nachricht(AntragStatuswechsel w)
        {
            var (leistung, service, niveau) = w.Typ == AntragTyp.Reisepass
                // lesbar wie der Antrag selbst erst ab Niveau "substanziell"
                ? ("Antrag auf einen Reisepass", "Reisepass", 3)
                : ("Sperrmüll-Anmeldung", "Sperrmüll", 1);
            var anrede = string.IsNullOrWhiteSpace(w.Name) ? "Guten Tag," : $"Guten Tag {w.Name},";
            var text = $"""
                {anrede}

                der Status Ihres Antrags hat sich geändert.

                Vorgangsnummer: {w.AntragId}
                Leistung: {leistung}
                Status: {StatusText(w.Status)}
                Zeitpunkt: {PostkorbBenachrichtigung.Ortszeit(w.ZeitpunktUtc)}
                Behörde: {Absender}

                {Hinweis(w)}

                Diese Statusmeldung entstand durch die simulierte Bearbeitung des Studienprojekts (es gibt keine Sachbearbeitung).
                """.ReplaceLineEndings("\n");
            return new PostkorbMessage(w.PostkorbHandle, $"Statusmeldung: {leistung} – {StatusText(w.Status)}", text, service, niveau);
        }

        /// <summary>Benachrichtigung aufs Handy: nur Leistung und Status, ein Tipp öffnet den Antrag im Portal.</summary>
        public static PushNotification Benachrichtigung(AntragStatuswechsel w)
        {
            var (leistung, ziel) = w.Typ == AntragTyp.Reisepass
                ? ("Antrag auf einen Reisepass", $"/Antraege/Antrag/{w.AntragId}")
                : ("Sperrmüll-Anmeldung", $"/Antraege/SperrmuellAntrag/{w.AntragId}");
            return new PushNotification($"{leistung}: {StatusText(w.Status)}", "Statusmeldung im BundID-Postfach", ziel,
                $"antrag-{w.AntragId}");
        }

        public static string StatusText(AntragStatus status) => status switch
        {
            AntragStatus.Eingereicht => "eingereicht",
            AntragStatus.InPruefung => "in Bearbeitung",
            AntragStatus.Genehmigt => "abgeschlossen",
            AntragStatus.Abgelehnt => "abgelehnt",
            _ => "Entwurf",
        };

        private static string Hinweis(AntragStatuswechsel w) => (w.Typ, w.Status) switch
        {
            (AntragTyp.Reisepass, AntragStatus.InPruefung) =>
                "Wir prüfen Ihre Angaben. Für Unterschrift und Fingerabdrücke brauchen Sie einen Termin im Bürgeramt – falls noch nicht geschehen, buchen Sie ihn im BürgerPortal.",
            (AntragTyp.Reisepass, AntragStatus.Genehmigt) =>
                "Ihr Reisepass ist abholbereit (Simulation – es wird kein Pass hergestellt).",
            (AntragTyp.Sperrmuell, AntragStatus.InPruefung) =>
                "Wir prüfen Ihren Wunschtermin.",
            (AntragTyp.Sperrmuell, AntragStatus.Genehmigt) =>
                w.WunschzeitUtc is { } tag
                    ? $"Die Abholung ist für Ihren Wunschtag bestätigt: {tag.ToString("dddd, dd.MM.yyyy", PostkorbBenachrichtigung.Deutsch)}."
                    : "Die Abholung ist bestätigt.",
            _ => string.Empty,
        };
    }
}
