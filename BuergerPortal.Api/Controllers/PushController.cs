using BuergerPortal.Api.Push;
using BuergerPortal.Application.Interfaces.Repositories;
using BuergerPortal.Domain.Push.Entity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    /// <summary>
    /// Benachrichtigungen aufs Handy (Web Push): öffentlicher VAPID-Schlüssel für den Browser, Abos der angemeldeten
    /// Person anlegen und entfernen, Test-Benachrichtigung. Ohne VAPID-Schlüssel antwortet alles mit 404.
    /// </summary>
    [ApiController]
    [Route("api/push")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public sealed class PushController(IOptions<PushOptions> options, IPushSubscriptionRepository subscriptions, PushNotifier notifier)
        : ControllerBase
    {
        /// <summary>So schickt der Browser sein Abo (PushSubscription.toJSON()).</summary>
        public sealed record SubscriptionKeys(string? P256dh, string? Auth);
        public sealed record SubscriptionRequest(string? Endpoint, SubscriptionKeys? Keys);

        private const int MaxEndpointLength = 800;   // wie in der Datenbank (Index)

        private bool TryGetUserId(out Guid id) => Guid.TryParse(User.FindFirstValue("sub"), out id);

        /// <summary>Öffentlicher VAPID-Schlüssel (applicationServerKey für pushManager.subscribe).</summary>
        [HttpGet("key")]
        [AllowAnonymous]
        public IActionResult Key() =>
            options.Value.IsConfigured ? Ok(new { publicKey = options.Value.VapidPublicKey }) : NotFound();

        [HttpPost("subscriptions")]
        [EnableRateLimiting(PushRateLimitPolicy.Name)]
        public async Task<IActionResult> Subscribe([FromBody] SubscriptionRequest request, CancellationToken ct)
        {
            if (!options.Value.IsConfigured) return NotFound();
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (Validate(request) is { } error) return Problem(statusCode: StatusCodes.Status400BadRequest, detail: error);

            await subscriptions.SaveAsync(new PushSubscription
            {
                UserId = userId,
                Endpoint = request.Endpoint!,
                P256dh = request.Keys!.P256dh!,
                Auth = request.Keys.Auth!,
            }, ct);
            return NoContent();
        }

        [HttpDelete("subscriptions")]
        public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            return await subscriptions.DeleteAsync(userId, endpoint, ct) ? NoContent() : NotFound();
        }

        /// <summary>Test-Benachrichtigung an alle Geräte der Person.</summary>
        [HttpPost("test")]
        [EnableRateLimiting(PushRateLimitPolicy.Name)]
        public async Task<IActionResult> Test(CancellationToken ct)
        {
            if (!options.Value.IsConfigured) return NotFound();
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var sent = await notifier.NotifyAsync(userId, new PushNotification("BürgerPortal",
                "Test: So sehen Benachrichtigungen des BürgerPortals aus.", "/Settings", "test"), ct);
            return Ok(new { sent });
        }

        private string? Validate(SubscriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint) || request.Endpoint.Length > MaxEndpointLength
                || !Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint) || !options.Value.IsAllowedEndpoint(endpoint))
            {
                return "Adresse des Abos fehlt oder gehört zu keinem bekannten Push-Dienst.";
            }
            if (!TryDecode(request.Keys?.P256dh, 65, out var p256dh) || !IsCurvePoint(p256dh) || !TryDecode(request.Keys?.Auth, 16, out _))
            {
                return "Schlüssel des Abos (p256dh, auth) fehlen oder sind ungültig.";
            }
            return null;
        }

        private static bool TryDecode(string? base64Url, int length, out byte[] bytes)
        {
            bytes = new byte[length];
            return base64Url is { Length: <= 100 } && Base64Url.TryDecodeFromChars(base64Url, bytes, out var written) && written == length;
        }

        // P-256 (FIPS 186-4, SEC 2): y² = x³ − 3x + b (mod p)
        private static readonly BigInteger P = BigInteger.Parse("0FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF", NumberStyles.HexNumber);
        private static readonly BigInteger B = BigInteger.Parse("05AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B", NumberStyles.HexNumber);

        /// <summary>
        /// Unkomprimierter Punkt auf P-256? Sonst schlägt später die Verschlüsselung fehl. Selbst gerechnet, weil die
        /// Kryptografie von Windows und Linux ungültige Punkte mit unterschiedlichen Ausnahmen ablehnt.
        /// </summary>
        private static bool IsCurvePoint(byte[] publicKey)
        {
            if (publicKey[0] != 0x04) return false;
            var x = new BigInteger(publicKey.AsSpan(1, 32), isUnsigned: true, isBigEndian: true);
            var y = new BigInteger(publicKey.AsSpan(33, 32), isUnsigned: true, isBigEndian: true);
            if (x >= P || y >= P) return false;
            var right = ((BigInteger.ModPow(x, 3, P) - 3 * x + B) % P + P) % P;
            return BigInteger.ModPow(y, 2, P) == right;
        }
    }
}
