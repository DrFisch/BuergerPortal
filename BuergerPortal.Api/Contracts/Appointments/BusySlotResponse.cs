namespace BuergerPortal.Api.Contracts.Appointments
{
    public sealed class BusySlotResponse
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }
}
