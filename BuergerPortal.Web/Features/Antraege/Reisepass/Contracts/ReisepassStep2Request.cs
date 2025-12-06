namespace BuergerPortal.Web.Features.Antraege.Reisepass.Contracts
{
    public sealed class ReisepassStep2Request
    {
        public bool Express { get; set; }
        public bool AltpassVorhanden { get; set; }
        public string? Hinweis { get; set; }
    }
}
