using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuergerPortal.Web.Controllers
{
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = "/")
        {
            return Challenge(
                    new AuthenticationProperties { RedirectUri = returnUrl },
                    OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        public IActionResult Logout()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
