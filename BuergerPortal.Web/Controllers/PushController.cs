using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    [Route("push")]
    public class PushController : Controller
    {
        private static readonly List<string> Subscriptions = new();

        private readonly IConfiguration _cfg;
        public PushController(IConfiguration cfg) => _cfg = cfg;

        [HttpGet("publickey")]
        public IActionResult GetPublicKey()
        { 
            return Ok(_cfg["Push:VapidPublicKey"] ?? ""); 
        }

        [HttpPost("subscribe")]
        public IActionResult Subscribe([FromBody] object subscriptionJson)
        {
            if (subscriptionJson is null) return BadRequest();
            var json = subscriptionJson.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(json) && !Subscriptions.Contains(json))
                Subscriptions.Add(json);
            return Ok(new { success = true });
        }
    }
}
