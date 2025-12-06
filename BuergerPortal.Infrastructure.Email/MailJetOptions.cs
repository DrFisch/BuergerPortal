namespace BuergerPortal.Infrastructure.Email
{
    public sealed class MailJetOptions
    {
        public string ApiKey { get; set; } = default!;
        public string SecretKey { get; set; } = default!;
        public string FromEmail { get; set; } = default!;
        public string FromName { get; set; } = "Bürgerportal";
    }
}
