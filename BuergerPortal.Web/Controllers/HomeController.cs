using BuergerPortal.Web.Features.Home.ViewModels;
using BuergerPortal.Web.Features.Termine.Contracts;
using BuergerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BuergerPortal.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _cf;
        private static readonly TimeZoneInfo BerlinTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory cf)
        {
            _logger = logger;
            _cf = cf;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var client = _cf.CreateClient("BuergerPortalApi");

            NextAppointmentVm? nextAppointment = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var res = await client.GetAsync("api/appointments/mine", ct);

                    if (res.IsSuccessStatusCode)
                    {
                        var apiItems = await res.Content
                            .ReadFromJsonAsync<List<AppointmentListItemResponse>>(cancellationToken: ct)
                            ?? new();

                        var upcoming = apiItems
                            .Where(t => !t.Cancelled && t.StartUtc > DateTime.UtcNow)
                            .OrderBy(t => t.StartUtc)
                            .FirstOrDefault();

                        if (upcoming is not null)
                        {
                            DateTime ToBerlin(DateTime utc) =>
                                TimeZoneInfo.ConvertTimeFromUtc(
                                    utc.Kind == DateTimeKind.Utc
                                        ? utc
                                        : DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                                    BerlinTz);

                            var startLocal = ToBerlin(upcoming.StartUtc);
                            var endLocal = ToBerlin(upcoming.EndUtc);

                            nextAppointment = new NextAppointmentVm
                            {
                                Id = upcoming.Id,
                                Service = upcoming.Service,
                                Location = upcoming.Location,
                                Uhrzeit= $"{startLocal:HH:mm}",
                                Datum = startLocal.Date
                            };
                        }
                    }
                    else
                    {
                        // Log useful debugging info so the UI can be diagnosed
                        var content = await res.Content.ReadAsStringAsync(ct);
                        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogInformation("Appointments API returned 401 Unauthorized when fetching /api/appointments/mine. User authenticated: {Authenticated}. Response: {Content}",
                                User.Identity?.IsAuthenticated, content);
                        }
                        else
                        {
                            _logger.LogWarning("Appointments API returned {Status} when fetching /api/appointments/mine. Response: {Content}", res.StatusCode, content);
                        }
                    }
                    // 401 -> einfach nichts anzeigen, kein Redirect auf Startseite
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Index: request for appointments was cancelled by the client.");
                }
                catch (Exception ex)
                {
                    // Fehler auf der Startseite lieber verschlucken, statt den Nutzer mit Fehlerseiten zu nerven
                    _logger.LogError(ex, "Fehler beim Laden der nächsten Termine (Index).");
                }
            }

            var vm = new HomeIndexVm
            {
                NextAppointment = nextAppointment
            };

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Datenschutz()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [HttpGet("/home/testuser")]
        public IActionResult TestUser()
        {
            var lines = User.Claims.Select(c => $"{c.Type} = {c.Value}");
            return Content(string.Join("\n", lines));
        }
    }
}
