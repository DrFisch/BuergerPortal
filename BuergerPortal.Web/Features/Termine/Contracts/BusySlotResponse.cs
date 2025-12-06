namespace BuergerPortal.Web.Features.Termine.Contracts
{
    public sealed class BusySlotResponse
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }
}
