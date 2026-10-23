using BuergerPortal.Web.Controllers;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Rücksprungziel nach dem Login (Open-Redirect-Schutz aus 690ca0e): Das Portal übernimmt <c>returnUrl</c> nur, wenn
    /// es eine Adresse dieses Portals ist – sonst geht es nach der BundID-Anmeldung zur Startseite. Sonst könnte ein
    /// Angreifer einen echten Anmeldelink des Portals verschicken, der nach der Anmeldung auf seine eigene Seite führt.
    /// </summary>
    public class RuecksprungzielTests
    {
        private static T MitUrlHelper<T>(T controller) where T : Controller
        {
            var http = new DefaultHttpContext();
            http.Request.Scheme = "https";
            http.Request.Host = new HostString("bpsimulation.example");
            var action = new ActionContext(http, new RouteData(), new ControllerActionDescriptor());
            controller.ControllerContext = new ControllerContext(action);
            controller.Url = new UrlHelper(action);
            return controller;
        }

        public static TheoryData<string?, string> Ziele => new()
        {
            { "/Termine", "/Termine" },
            { "/Antraege/Status?filter=laufend", "/Antraege/Status?filter=laufend" },
            { "~/MeineDaten", "~/MeineDaten" },
            { "https://boese.example/phishing", "/" },
            { "http://bpsimulation.example/Termine", "/" },     // auch der eigene Host absolut: nicht lokal
            { "//boese.example", "/" },                         // protokollrelativ = fremder Host
            { "/\\boese.example", "/" },                        // Browser lesen "\" wie "/"
            { "javascript:alert(1)", "/" },
            { "", "/" },
            { null, "/" },
        };

        [Theory]
        [MemberData(nameof(Ziele))]
        public void AccountController_Login_nimmt_nur_lokale_Ziele(string? returnUrl, string erwartet)
        {
            var result = Assert.IsType<ChallengeResult>(MitUrlHelper(new AccountController()).Login(returnUrl));

            Assert.Equal(erwartet, result.Properties!.RedirectUri);
            Assert.Equal([OpenIdConnectDefaults.AuthenticationScheme], result.AuthenticationSchemes);
        }

        [Theory]
        [MemberData(nameof(Ziele))]
        public void AuthController_Login_nimmt_nur_lokale_Ziele(string? returnUrl, string erwartet)
        {
            var result = Assert.IsType<ChallengeResult>(MitUrlHelper(new AuthController()).Login(returnUrl));

            Assert.Equal(erwartet, result.Properties!.RedirectUri);
        }

        [Theory]
        [InlineData("/Termine", "/Termine")]
        [InlineData("https://boese.example", null)]
        [InlineData("//boese.example", null)]
        public void AuthController_LoginRequired_gibt_nur_lokale_Ziele_weiter(string returnUrl, string? erwartet)
        {
            var result = Assert.IsType<RedirectToActionResult>(MitUrlHelper(new AuthController()).LoginRequired(returnUrl));

            Assert.Equal("Index", result.ActionName);
            Assert.Equal(erwartet, result.RouteValues?["returnUrl"] as string);
        }
    }
}
