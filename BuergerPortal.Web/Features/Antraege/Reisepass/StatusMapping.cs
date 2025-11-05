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
        public static (string text, string badge, int progress) Map(int statusInt)
        {
            var s = (AntragStatus)statusInt;
            return s switch
            {
                AntragStatus.Entwurf => ("Entwurf", "text-bg-secondary", 25),
                AntragStatus.Eingereicht => ("Eingereicht", "text-bg-info", 50),
                AntragStatus.InPruefung => ("In Prüfung", "text-bg-warning", 75),
                AntragStatus.Genehmigt => ("Genehmigt", "text-bg-success", 100),
                AntragStatus.Abgelehnt => ("Abgelehnt", "text-bg-danger", 100),
                _ => ("Unbekannt", "text-bg-secondary", 0)
            };
        }
    }
}
