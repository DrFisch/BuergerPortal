namespace BuergerPortal.Api.Contracts.Maengelmeldungen
{
    public sealed class CreateMaengelmeldungResponse
    {
        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Status { get; set; } = default!;
    }
}
