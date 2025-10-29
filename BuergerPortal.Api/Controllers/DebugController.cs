using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        [HttpGet("auth-header")]
        public object AuthHeader()
        {
            var hasAuth = Request.Headers.TryGetValue("Authorization", out var auth);
            return new { hasAuth, auth = hasAuth ? auth.ToString() : null };
        }

        [HttpGet("whoami")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public object WhoAmI() => new
        {
            Authenticated = User.Identity?.IsAuthenticated,
            Sub = User.FindFirst("sub")?.Value,
            Scope = User.FindFirst("scope")?.Value,
            Aud = User.FindAll("aud").Select(a => a.Value).ToArray()
        };
    }

}
