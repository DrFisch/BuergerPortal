using BuergerPortal.Application.Interfaces.Mail;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace BuergerPortal.Infrastructure.Email
{
    public sealed class MailJetEmailSender : IEmailSender
    {
        private readonly MailJetOptions _opt;
        private readonly MailjetClient _client;

        public MailJetEmailSender(IOptions<MailJetOptions> opt)
        {
            _opt = opt.Value;
            _client = new MailjetClient(_opt.ApiKey, _opt.SecretKey); // kein ClientOptions nötig
        }

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            var request = new MailjetRequest { Resource = Send.Resource }
                .Property(Send.FromEmail, _opt.FromEmail)
                .Property(Send.FromName, _opt.FromName)
                .Property(Send.Subject, subject)
                .Property(Send.HtmlPart, htmlBody)
                .Property(Send.TextPart, "Ihr E-Mail-Client unterstützt kein HTML.")
                .Property(Send.Recipients, new JArray {
                    new JObject { { "Email", to } }
                });

            var response = await _client.PostAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var details = response.GetErrorMessage() ?? response.GetData()?.ToString();
                throw new InvalidOperationException($"Mailjet Error {response.StatusCode}: {details}");
            }
        }
    }
}
