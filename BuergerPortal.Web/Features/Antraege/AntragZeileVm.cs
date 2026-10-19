namespace BuergerPortal.Web.Features.Antraege
{
    /// <summary>
    /// Ein eigener Antrag als Zeile – gleich für alle Antragsarten (Übersicht, „Meine Anträge“). Status wie die API:
    /// 1 Entwurf, 2 eingereicht, 3 in Bearbeitung, 4 abgeschlossen (genehmigt), 5 abgelehnt.
    /// </summary>
    public sealed class AntragZeileVm
    {
        public Guid Id { get; init; }
        public int Typ { get; init; }
        public int Status { get; init; }
        public string Antragsteller { get; init; } = string.Empty;
        public DateTime Angelegt { get; init; }
        public DateTime? Eingereicht { get; init; }

        public LeistungInfo? Leistung => Leistungen.Von(Typ);
        public bool IstEntwurf => Status == 1;
        public bool IstLaufend => Status is 2 or 3;
        public bool IstErledigt => Status is 4 or 5;

        /// <summary>Erreichte Stufe der drei Stufen eingereicht → in Bearbeitung → abgeschlossen (0 = Entwurf).</summary>
        public int Stufe => Status switch { 2 => 1, 3 => 2, 4 or 5 => 3, _ => 0 };

        /// <summary>Kurze Vorgangsnummer zum Wiedererkennen (erste 8 Zeichen der Id).</summary>
        public string Vorgangsnummer => Id.ToString("N")[..8].ToUpperInvariant();

        /// <summary>Zeitpunkt für die Sortierung: eingereicht, sonst angelegt.</summary>
        public DateTime Zeitpunkt => Eingereicht ?? Angelegt;
    }

    public sealed class AntraegeUebersichtVm
    {
        public IReadOnlyList<AntragZeileVm> Antraege { get; init; } = [];

        /// <summary>Erreichtes BundID-Vertrauensniveau der Anmeldung (1, 3, 4).</summary>
        public int Niveau { get; init; }

        /// <summary>Geburtsdatum aus der BundID (für die persönliche Reisepassgebühr), sonst null.</summary>
        public DateOnly? Geburtsdatum { get; init; }

        public IEnumerable<AntragZeileVm> Entwuerfe => Antraege.Where(a => a.IstEntwurf);
        public IEnumerable<AntragZeileVm> Aktuelle => Antraege.Where(a => !a.IstEntwurf);
    }

    /// <summary>„Meine Anträge“: alle eigenen Anträge mit Filter (alle, laufend, entwuerfe, erledigt).</summary>
    public sealed class AntraegeStatusVm
    {
        public static readonly IReadOnlyList<string> Filternamen = ["alle", "laufend", "entwuerfe", "erledigt"];

        public IReadOnlyList<AntragZeileVm> Antraege { get; init; } = [];
        public string Filter { get; init; } = "alle";

        /// <summary>Unbekannte oder fehlende Filter zeigen alle Anträge.</summary>
        public static string Normalisieren(string? filter) =>
            filter is not null && Filternamen.Contains(filter) ? filter : "alle";

        public IEnumerable<AntragZeileVm> Nach(string filter) => filter switch
        {
            "laufend" => Antraege.Where(a => a.IstLaufend),
            "entwuerfe" => Antraege.Where(a => a.IstEntwurf),
            "erledigt" => Antraege.Where(a => a.IstErledigt),
            _ => Antraege,
        };

        public IReadOnlyList<AntragZeileVm> Gefiltert => Nach(Filter).ToList();
    }
}
