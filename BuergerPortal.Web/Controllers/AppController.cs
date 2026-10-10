using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace BuergerPortal.Web.Controllers
{
    /// <summary>
    /// Hilfen rund um die App (PWA). QR-Code mit der Adresse des Portals für den Dialog „App installieren“: Am Computer
    /// scannt man ihn mit dem Handy und öffnet das Portal dort. Als SVG vom eigenen Server – ohne fremdes Skript, passend
    /// zur Content-Security-Policy (img-src 'self').
    /// </summary>
    [AllowAnonymous]
    public class AppController : Controller
    {
        [HttpGet("/app/qr.svg")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public IActionResult Qr()
        {
            // Startadresse des Portals, wie der Browser sie sieht (hinter Caddy über die Forwarded Headers)
            var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/";
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var svg = new SvgQRCode(data).GetGraphic(4);
            return Content(svg, "image/svg+xml");
        }
    }
}
