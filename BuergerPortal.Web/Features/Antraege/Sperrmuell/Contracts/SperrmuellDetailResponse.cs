using BuergerPortal.Web.Features.Antraege.Reisepass;
using static BuergerPortal.Web.Features.Antraege.Reisepass.AntragStatusUi;

namespace BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts
{
    public sealed class SperrmuellDetailResponse
    {
        public Guid Id { get; set; }
        public int Typ { get; set; }
        public AntragStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? SubmittedUtc { get; set; }

        public string Vorname { get; set; } = string.Empty;
        public string Nachname { get; set; } = string.Empty;
        public DateOnly Geburtsdatum { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }

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
