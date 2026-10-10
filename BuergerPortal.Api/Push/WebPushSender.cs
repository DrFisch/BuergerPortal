using BuergerPortal.Domain.Push.Entity;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BuergerPortal.Api.Push
{
    /// <summary>Inhalt einer Benachrichtigung; der Service Worker (sw.js) zeigt Titel und Text, ein Tipp öffnet <c>Url</c>.</summary>
    public sealed record PushNotification(string Title, string Body, string Url, string Tag);

    public enum PushSendResult
    {
        Sent,
        // Push-Dienst kennt das Abo nicht mehr (404/410) – z. B. Berechtigung entzogen, App gelöscht → Abo löschen
        Gone,
        Failed,
    }

    /// <summary>
    /// Schickt eine Benachrichtigung an ein Abo: Inhalt nach RFC 8291 verschlüsselt (<see cref="WebPushEncryption"/>),
    /// Absender per VAPID ausgewiesen (<see cref="Vapid"/>), Versand per HTTP POST an den Push-Dienst (RFC 8030).
    /// </summary>
    public sealed class WebPushSender(HttpClient http, IOptions<PushOptions> options, TimeProvider time, ILogger<WebPushSender> logger)
    {
        /// <summary>So lange hält der Push-Dienst die Nachricht vor, wenn das Gerät gerade offline ist.</summary>
        public static readonly TimeSpan TimeToLive = TimeSpan.FromDays(1);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public async Task<PushSendResult> SendAsync(PushSubscription subscription, PushNotification notification, CancellationToken ct)
        {
            var o = options.Value;
            if (!o.IsConfigured || !Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint) || !o.IsAllowedEndpoint(endpoint))
            {
                logger.LogWarning("Push nicht versendet: nicht eingerichtet oder Push-Dienst nicht erlaubt");
                return PushSendResult.Failed;
            }

            var body = WebPushEncryption.Encrypt(JsonSerializer.SerializeToUtf8Bytes(notification, Json),
                Base64Url.DecodeFromChars(subscription.P256dh), Base64Url.DecodeFromChars(subscription.Auth));
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("TTL", ((int)TimeToLive.TotalSeconds).ToString());
            request.Headers.TryAddWithoutValidation("Urgency", "normal");
            request.Headers.TryAddWithoutValidation("Authorization", Vapid.AuthorizationHeader(endpoint, o.Subject!,
                Base64Url.DecodeFromChars(o.VapidPublicKey), Base64Url.DecodeFromChars(o.VapidPrivateKey), time.GetUtcNow()));

            try
            {
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return PushSendResult.Sent;
                }
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    return PushSendResult.Gone;
                }
                logger.LogWarning("Push-Dienst {Host} lehnt ab: {Status}", endpoint.Host, (int)response.StatusCode);
                return PushSendResult.Failed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Push-Dienst {Host} nicht erreichbar: {Message}", endpoint.Host, ex.Message);
                return PushSendResult.Failed;
            }
        }
    }
}
