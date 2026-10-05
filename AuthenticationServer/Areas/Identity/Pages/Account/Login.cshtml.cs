// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthenticationServer.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Anmeldeseite des Auth-Servers. Die Anmeldung erfolgt nur noch über die BundID;
    /// der frühere Login mit E-Mail und Passwort ist entfallen.
    /// </summary>
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        public string ReturnUrl { get; set; }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
        }

        // Alte Formulare (Passwort-Login) landen ebenfalls bei der BundID-Anmeldung.
        public IActionResult OnPost(string returnUrl = null)
        {
            OnGet(returnUrl);
            return Redirect($"/bundid/login?returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }
    }
}
