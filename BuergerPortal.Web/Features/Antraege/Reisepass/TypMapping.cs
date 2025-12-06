namespace BuergerPortal.Web.Features.Antraege.Reisepass
{
    public enum AntragTypUi
    {
        Reisepass = 1,
        Sperrmuell = 2
    }

    public static class AntragTypUiMap
    {
        public static (string text, string badgeClass) Map(int typInt)
        {
            var t = Enum.IsDefined(typeof(AntragTypUi), typInt)
                ? (AntragTypUi)typInt
                : 0;

            return t switch
            {
                AntragTypUi.Reisepass => ("Reisepass", "bg-primary-subtle text-primary-emphasis"),
                AntragTypUi.Sperrmuell => ("Sperrmüll", "bg-success-subtle text-success-emphasis"),
                _ => ("Unbekannt", "bg-secondary-subtle text-secondary-emphasis")
            };
        }
    }
}
