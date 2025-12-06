namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts
{
    public sealed class SperrmuellStep2Request
    {
        public string Strasse { get; set; } = string.Empty;
        public string PLZ { get; set; } = string.Empty;
        public string Ort { get; set; } = string.Empty;

        public int? HolzKubikmeter { get; set; }
        public int? SonstigesKubikmeter { get; set; }
        public int? Matratzen { get; set; }

        public DateTime Wunschzeit { get; set; }
        public string? Hinweis { get; set; }
    }
}
