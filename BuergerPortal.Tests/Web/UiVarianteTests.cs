using BuergerPortal.Web.Controllers;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Portal 2 (E-027/E-028): Schalter <c>Ui:Modern</c> in den appsettings, Umschalter <c>?ui=</c> je Browser mit
    /// Cookie <c>__Host-bp-ui</c>, View-Suche unter <c>Views/Modern/</c> mit Rückfall auf die klassische Oberfläche.
    /// </summary>
    public class UiVarianteTests
    {
        private const string Kennzeichen = "<meta name=\"bp-ui\" content=\"modern\" />";

        private sealed class PortalFactory(bool? modern = null, bool? urlUmschalter = null) : WebApplicationFactory<HomeController>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Production");
                var werte = new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "http://api.invalid/",
                    ["Authentication:Authority"] = "http://auth.invalid/",
                    ["Authentication:ClientId"] = "test",
                    ["Authentication:ClientSecret"] = "test",
                };
                if (modern is { } m) werte["Ui:Modern"] = m ? "true" : "false";
                if (urlUmschalter is { } u) werte["Ui:UrlUmschalter"] = u ? "true" : "false";
                builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(werte));
            }
        }

        private static HttpClient Client(PortalFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        private static async Task<HttpResponseMessage> Get(PortalFactory factory, string url, string? cookie = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (cookie != null) request.Headers.Add("Cookie", $"{UiVariante.CookieName}={cookie}");
            return await Client(factory).SendAsync(request);
        }

        private static string? UiCookie(HttpResponseMessage response) =>
            response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.SingleOrDefault(c => c.StartsWith(UiVariante.CookieName + "=", StringComparison.Ordinal))
                : null;

        [Fact]
        public void Appsettings_Standard_ist_die_alte_Oberflaeche_mit_Url_Umschalter()
        {
            using var factory = new PortalFactory();
            var options = factory.Services.GetRequiredService<IOptionsMonitor<UiOptions>>().CurrentValue;

            Assert.False(options.Modern);
            Assert.True(options.UrlUmschalter);
            Assert.False(new UiOptions().Modern);
        }

        [Theory]
        [InlineData(false, true, null, false)]
        [InlineData(true, true, null, true)]
        [InlineData(false, true, "modern", true)]      // eigene Wahl schlägt appsettings
        [InlineData(true, true, "klassisch", false)]
        [InlineData(false, false, "modern", false)]    // URL-Umschalter aus: nur appsettings zählt
        [InlineData(true, false, "klassisch", true)]
        [InlineData(true, true, "bunt", true)]         // unbekannter Wert zählt nicht
        [InlineData(false, true, "MODERN", false)]
        public void Entscheidung_Cookie_vor_appsettings(bool modern, bool urlUmschalter, string? cookie, bool erwartet) =>
            Assert.Equal(erwartet, UiVariante.Decide(new UiOptions { Modern = modern, UrlUmschalter = urlUmschalter }, cookie));

        [Fact]
        public async Task Url_modern_merkt_die_Wahl_im_Sitzungscookie_und_leitet_ohne_ui_weiter()
        {
            using var factory = new PortalFactory();
            var response = await Get(factory, "/?ui=modern");

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/", response.Headers.Location?.OriginalString);
            var cookie = UiCookie(response);
            Assert.NotNull(cookie);
            Assert.StartsWith($"{UiVariante.CookieName}=modern;", cookie);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);   // endet mit dem Browser
            Assert.DoesNotContain("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Weiterleitung_behaelt_die_uebrigen_Parameter()
        {
            using var factory = new PortalFactory();
            var response = await Get(factory, "/Home/Impressum?a=1&ui=klassisch&b=zwei%20Worte");

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Home/Impressum?a=1&b=zwei%20Worte", response.Headers.Location?.OriginalString);
            Assert.StartsWith($"{UiVariante.CookieName}=klassisch;", UiCookie(response));
        }

        [Fact]
        public async Task Url_standard_loescht_die_Wahl()
        {
            using var factory = new PortalFactory();
            var response = await Get(factory, "/?ui=standard", cookie: "modern");

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            var cookie = UiCookie(response);
            Assert.NotNull(cookie);
            Assert.StartsWith($"{UiVariante.CookieName}=;", cookie);
            Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Unbekannter_Wert_wird_ignoriert()
        {
            using var factory = new PortalFactory();
            var response = await Get(factory, "/?ui=bunt");

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Null(UiCookie(response));
        }

        [Fact]
        public async Task Abgeschalteter_Url_Umschalter_ignoriert_ui_und_Cookie()
        {
            using var factory = new PortalFactory(modern: false, urlUmschalter: false);

            var mitUi = await Get(factory, "/?ui=modern");
            Assert.Equal(System.Net.HttpStatusCode.OK, mitUi.StatusCode);
            Assert.Null(UiCookie(mitUi));

            var mitCookie = await Get(factory, "/", cookie: "modern");
            Assert.DoesNotContain(Kennzeichen, await mitCookie.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Post_schaltet_nicht_um()
        {
            using var factory = new PortalFactory();
            var response = await Client(factory).PostAsync("/?ui=modern", new FormUrlEncodedContent([]));

            // Weiter geht es nur zur Anmeldung (Einstiegsseite mit ReturnUrl), nicht über den Umschalter
            Assert.Null(UiCookie(response));
            Assert.NotEqual("/", response.Headers.Location?.OriginalString);
        }

        [Theory]
        [InlineData("//boese.example/pfad", "/")]
        [InlineData("/\\boese.example", "/%5Cboese.example")]   // maskiert: bleibt ein Pfad
        [InlineData("/Termine", "/Termine")]
        public void Weiterleitung_bleibt_auf_dem_Portal(string pfad, string erwartet)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = pfad;
            context.Request.QueryString = new QueryString("?ui=modern");

            Assert.Equal(erwartet, UiVariante.AdresseOhneUi(context.Request));
        }

        [Fact]
        public async Task Modern_true_nimmt_das_Layout_von_Portal_2_und_faellt_bei_der_Seite_zurueck()
        {
            using var factory = new PortalFactory(modern: true);
            var html = await (await Get(factory, "/")).Content.ReadAsStringAsync();

            Assert.Contains(Kennzeichen, html);                 // Views/Modern/Shared/_LayoutModern.cshtml
            Assert.Contains("id=\"start-heading\"", html);     // Einstiegsseite: noch die klassische View
        }

        [Fact]
        public async Task Modern_false_liefert_die_alte_Oberflaeche()
        {
            using var factory = new PortalFactory(modern: false);
            var html = await (await Get(factory, "/")).Content.ReadAsStringAsync();

            Assert.DoesNotContain(Kennzeichen, html);
            Assert.DoesNotContain("bp-ui", html);
        }

        [Theory]
        [InlineData(false, "modern", true)]
        [InlineData(true, "klassisch", false)]
        public async Task Cookie_schlaegt_appsettings(bool modern, string cookie, bool erwartetModern)
        {
            using var factory = new PortalFactory(modern: modern);
            var html = await (await Get(factory, "/", cookie)).Content.ReadAsStringAsync();

            Assert.Equal(erwartetModern, html.Contains(Kennzeichen));
        }

        private static ViewLocationExpanderContext ExpanderContext(bool? modern)
        {
            var http = new DefaultHttpContext();
            if (modern is { } m) UiVariante.SetModern(http, m);
            var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
            return new ViewLocationExpanderContext(action, "Index", "Termine", areaName: null, pageName: null, isMainPage: true)
            {
                Values = new Dictionary<string, string?>(),   // legt sonst die View-Engine an
            };
        }

        private static readonly string[] Klassisch = ["/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml"];

        [Fact]
        public void View_Suche_modern_zuerst_unter_Views_Modern_dann_klassisch()
        {
            var expander = new ModernViewLocationExpander();
            var context = ExpanderContext(true);
            expander.PopulateValues(context);

            var orte = expander.ExpandViewLocations(context, Klassisch).ToList();

            Assert.Equal("modern", context.Values["ui"]);
            Assert.Equal(["/Views/Modern/{1}/{0}.cshtml", "/Views/Modern/Shared/{0}.cshtml", .. Klassisch], orte);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(null)]   // ohne Middleware: klassisch
        public void View_Suche_klassisch_nie_unter_Views_Modern(bool? modern)
        {
            var expander = new ModernViewLocationExpander();
            var context = ExpanderContext(modern);
            expander.PopulateValues(context);

            var orte = expander.ExpandViewLocations(context, Klassisch).ToList();

            Assert.Equal("klassisch", context.Values["ui"]);
            Assert.Equal(Klassisch, orte);
            Assert.DoesNotContain(orte, o => o.Contains("/Modern/", StringComparison.Ordinal));
        }
    }
}
