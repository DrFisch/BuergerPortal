namespace BuergerPortal.Web.Features.Antraege.Reisepass
{
    public static class AntragStatusUi
    {
        public enum AntragStatus
        {
            Entwurf = 1,
            Eingereicht = 2,
            InPruefung = 3,
            Genehmigt = 4,
            Abgelehnt = 5
        }

        // neue, eigentliche Logik
        public static (string text, string badge, int progress) Map(AntragStatus status)
        {
            return status switch
            {
                AntragStatus.Entwurf => ("Entwurf", "text-bg-secondary", 25),
                AntragStatus.Eingereicht => ("Eingereicht", "text-bg-info", 50),
                AntragStatus.InPruefung => ("In Prüfung", "text-bg-warning", 75),
                AntragStatus.Genehmigt => ("Genehmigt", "text-bg-success", 100),
                AntragStatus.Abgelehnt => ("Abgelehnt", "text-bg-danger", 100),
                _ => ("Unbekannt", "text-bg-secondary", 0)
            };
        }

        // alte Signatur behalten für int-Caller (API-Models)
        public static (string text, string badge, int progress) Map(int statusInt)
            => Map((AntragStatus)statusInt);
    }
}
