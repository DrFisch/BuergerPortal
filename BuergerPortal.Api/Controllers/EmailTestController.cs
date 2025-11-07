using BuergerPortal.Application.Interfaces.Mail;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Api.Controllers
{
    [ApiController]
    [Route("api/email")]
    public class EmailTestController : ControllerBase
    {
        private readonly IEmailSender _email;

        public EmailTestController(IEmailSender email)
        {
            _email = email;
        }

        [HttpPost("test")]
        public async Task<IActionResult> SendTest([FromBody] TestMailDto dto, CancellationToken ct)
        {
            await _email.SendAsync(
                dto.To,
                dto.Subject ?? "Test-Mail vom Bürgerportal",
                dto.HtmlBody ?? "<p>Hallo vom Bürgerportal 👋</p>",
                ct
            );

            return Ok($"E-Mail an {dto.To} gesendet!");
        }
    }

    public class TestMailDto
    {
        public string To { get; set; } = default!;
        public string? Subject { get; set; }
        public string? HtmlBody { get; set; }
    }
}