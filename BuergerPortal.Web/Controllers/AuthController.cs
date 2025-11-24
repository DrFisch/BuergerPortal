using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        [HttpGet]
        public IActionResult LoginRequired(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string? returnUrl = null)
        {
            var redirectUrl = string.IsNullOrEmpty(returnUrl) ? Url.Content("~/") : returnUrl;

            return Challenge(
                new AuthenticationProperties { RedirectUri = redirectUrl },
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
