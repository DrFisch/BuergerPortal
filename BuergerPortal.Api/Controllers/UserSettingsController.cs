using BuergerPortal.Api.Contracts.Settings;
using BuergerPortal.Domain.Settings.Entity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/users/me/settings")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public sealed class UserSettingsController : ControllerBase
    {
        private readonly IUserSettingsService _svc;
        public UserSettingsController(IUserSettingsService svc) => _svc = svc;

        private bool TryGetUserId(out Guid userId)
            => Guid.TryParse(User.FindFirstValue("sub"), out userId);

        [HttpGet]
        [ProducesResponseType(typeof(UserSettingsResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserSettingsResponse>> Get(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var s = await _svc.GetAsync(userId, ct) ?? new UserSettings { UserId = userId };
            var resp = ToResponse(s);

            // ETag (RowVersion) zurückgeben
            Response.Headers.ETag = $"W/\"{resp.Version}\"";
            return Ok(resp);
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        public async Task<IActionResult> Put([FromBody] UserSettingsUpdateRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized();

            var expected = TryReadIfMatch(out var tag) ? Base64ToBytes(tag)
                           : (req.Version is not null ? Convert.FromBase64String(req.Version) : null);

            try
            {
                var updated = await _svc.UpsertAsync(userId, new UserSettings
                {
                    Theme = SanitizeTheme(req.Theme),
                    Language = string.IsNullOrWhiteSpace(req.Language) ? "de" : req.Language,
                    PushEnabled = req.PushEnabled,
                    ReduceDataUsage = req.ReduceDataUsage,
                    AnalyticsOptIn = req.AnalyticsOptIn,
                    AllowGeolocation = req.AllowGeolocation
                }, expected, ct);

                Response.Headers.ETag = $"W/\"{Convert.ToBase64String(updated.RowVersion)}\"";
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Problem(title: "Version conflict", statusCode: StatusCodes.Status412PreconditionFailed,
                    detail: "Die Einstellungen wurden parallel geändert. Seite aktualisieren und erneut speichern.");
            }
        }

        private static string SanitizeTheme(string v) =>
            string.Equals(v, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";

        private static UserSettingsResponse ToResponse(UserSettings s) => new()
        {
            Theme = s.Theme,
            Language = s.Language,
            PushEnabled = s.PushEnabled,
            ReduceDataUsage = s.ReduceDataUsage,
            AnalyticsOptIn = s.AnalyticsOptIn,
            AllowGeolocation = s.AllowGeolocation,
            Version = Convert.ToBase64String(s.RowVersion ?? Array.Empty<byte>())
        };

        private bool TryReadIfMatch(out string base64)
        {
            base64 = "";
            if (!Request.Headers.TryGetValue("If-Match", out var h)) return false;
            var v = h.ToString();
            var i = v.IndexOf('"');
            if (i < 0) return false;
            base64 = v.Trim().Trim('W', '/', '"');
            return !string.IsNullOrWhiteSpace(base64);
        }
        private static byte[]? Base64ToBytes(string b64)
        {
            try { return Convert.FromBase64String(b64); } catch { return null; }
        }
    }

}
