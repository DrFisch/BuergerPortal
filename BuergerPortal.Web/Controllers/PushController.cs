using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// Benachrichtigungen aufs Handy (Web Push): reicht die Aufrufe von wwwroot/js/push.js mit dem Access-Token der
    /// Sitzung an die API weiter (api/push). Ändernde Aufrufe verlangen das Antiforgery-Token – sonst könnte eine fremde
    /// Seite der angemeldeten Person ein eigenes Abo unterschieben und deren Benachrichtigungen mitlesen.
    /// </summary>
    [Authorize]
    [Route("push")]
    public sealed class PushController(IHttpClientFactory httpClientFactory, ILogger<PushController> logger) : Controller
    {
        private HttpClient Api => httpClientFactory.CreateClient("BuergerPortalApi");

        /// <summary>Öffentlicher VAPID-Schlüssel; 404, wenn Push nicht eingerichtet ist.</summary>
        [HttpGet("key")]
        [AllowAnonymous]
        public Task<IActionResult> Key(CancellationToken ct) => Forward(() => Api.GetAsync("api/push/key", ct), ct);

        [HttpPost("subscriptions")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(4096)]
        public Task<IActionResult> Subscribe([FromBody] JsonElement subscription, CancellationToken ct) =>
            Forward(() => Api.PostAsJsonAsync("api/push/subscriptions", subscription, ct), ct);

        [HttpDelete("subscriptions")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Unsubscribe([FromQuery] string endpoint, CancellationToken ct) =>
            Forward(() => Api.DeleteAsync("api/push/subscriptions?endpoint=" + Uri.EscapeDataString(endpoint), ct), ct);

        [HttpPost("test")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Test(CancellationToken ct) => Forward(() => Api.PostAsync("api/push/test", null, ct), ct);

        // Status und Inhalt der API unverändert zurück (ProblemDetails mit detail zeigt push.js an)
        private async Task<IActionResult> Forward(Func<Task<HttpResponseMessage>> call, CancellationToken ct)
        {
            try
            {
                using var response = await call();
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    Content = await response.Content.ReadAsStringAsync(ct),
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                };
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("API für Benachrichtigungen nicht erreichbar: {Message}", ex.Message);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }
    }
}
