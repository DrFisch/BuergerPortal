using BuergerPortal.Api.Contracts.Settings;
using BuergerPortal.Application.Common;
using BuergerPortal.Application.Interfaces.UserEinstellungen;
using BuergerPortal.Application.UserEinstellungen.DTOs;
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
        private readonly IUserSettingsBusinessService _bs;
        public UserSettingsController(IUserSettingsBusinessService bs) => _bs = bs;

        private bool TryGetUserId(out Guid id) =>
            Guid.TryParse(User.FindFirstValue("sub"), out id);

        [HttpGet]
        [ProducesResponseType(typeof(UserSettingsResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<UserSettingsResponse>> Get(CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var dto = await _bs.GetForUserAsync(userId, ct);
            var resp = new UserSettingsResponse
            {
                Theme = dto.Theme,
                Language = dto.Language,
                PushEnabled = dto.PushEnabled,
                ReduceDataUsage = dto.ReduceDataUsage,
                AnalyticsOptIn = dto.AnalyticsOptIn,
                AllowGeolocation = dto.AllowGeolocation,
                Version = dto.Version
            };

            if (!string.IsNullOrEmpty(resp.Version))
                Response.Headers.ETag = $"W/\"{resp.Version}\"";

            return Ok(resp);
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Put([FromBody] UserSettingsUpdateRequest req, CancellationToken ct)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            // If-Match bevorzugen; sonst Body.Version
            byte[]? expected = null;
            if (Request.Headers.TryGetValue("If-Match", out var ifm))
            {
                var raw = ifm.ToString().Trim().Trim('W', '/', '"');
                if (!string.IsNullOrWhiteSpace(raw))
                    try { expected = Convert.FromBase64String(raw); } catch { }
            }
            if (expected is null && !string.IsNullOrWhiteSpace(req.Version))
            {
                try { expected = Convert.FromBase64String(req.Version!); } catch { }
            }

            var dto = new UserSettingsUpdateDto
            {
                Theme = req.Theme,
                Language = req.Language,
                PushEnabled = req.PushEnabled,
                ReduceDataUsage = req.ReduceDataUsage,
                AnalyticsOptIn = req.AnalyticsOptIn,
                AllowGeolocation = req.AllowGeolocation,
                ExpectedVersion = expected
            };

            var result = await _bs.UpsertForUserAsync(userId, dto, ct);
            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    ErrorCodes.Validation => ValidationProblem(detail: result.ErrorMessage),
                    ErrorCodes.Concurrency => Problem(
                                                  title: "Version conflict",
                                                  statusCode: StatusCodes.Status412PreconditionFailed,
                                                  detail: result.ErrorMessage),
                    ErrorCodes.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden, detail: result.ErrorMessage),
                    ErrorCodes.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, detail: result.ErrorMessage),
                    _ => Problem(statusCode: StatusCodes.Status400BadRequest, detail: result.ErrorMessage)
                };
            }

            // Erfolg

            // neue ETag zurückgeben (erneut GET wäre sauber; hier optionaler Roundtrip vermeiden):
            var fresh = await _bs.GetForUserAsync(userId, ct);
            if (!string.IsNullOrEmpty(fresh.Version))
                Response.Headers.ETag = $"W/\"{fresh.Version}\"";

            return NoContent();
        }
    }
}
