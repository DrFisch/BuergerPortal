using System.Net.Http.Json;
using BuergerPortal.Application.Interfaces.Postkorb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Infrastructure.Postkorb
{
    /// <summary>
    /// Stellt Nachrichten über POST api/v1/messages der Postkorb-Simulation zu (Felder nach ZBP "CreateMessage").
    /// Basisadresse, API-Schlüssel und Timeout setzt die Registrierung am HttpClient.
    /// </summary>
    public sealed class HttpPostkorbService(HttpClient http, IOptions<PostkorbOptions> options,
        ILogger<HttpPostkorbService> logger) : IPostkorbService
    {
        public const string ApiKeyHeader = "X-Api-Key";

        private sealed record CreateMessageBody(Guid MailboxUuid, string Title, string Content, string Sender,
            string Service, string? ReplyAddress, int StorkQaaLevel);

        public async Task<PostkorbDeliveryStatus> SendAsync(PostkorbMessage message, CancellationToken ct = default)
        {
            if (!Guid.TryParse(message.PostkorbHandle, out var handle) || handle == Guid.Empty)
            {
                return PostkorbDeliveryStatus.NoMailbox;
            }

            var o = options.Value;
            var body = new CreateMessageBody(handle, message.Title, message.Content, o.Sender, message.Service,
                o.ReplyAddress, message.StorkQaaLevel);
            try
            {
                using var response = await http.PostAsJsonAsync("api/v1/messages", body, ct);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Postkorb-Nachricht zugestellt ({Service})", message.Service);
                    return PostkorbDeliveryStatus.Delivered;
                }
                logger.LogWarning("Postkorb hat die Nachricht abgelehnt: HTTP {StatusCode} ({Service})",
                    (int)response.StatusCode, message.Service);
                return PostkorbDeliveryStatus.Failed;
            }
            catch (Exception ex)
            {
                // Nicht erreichbar, Timeout, Zertifikatsfehler … – der Fachvorgang läuft trotzdem weiter.
                logger.LogWarning(ex, "Postkorb nicht erreichbar ({Service})", message.Service);
                return PostkorbDeliveryStatus.Failed;
            }
        }
    }
}
