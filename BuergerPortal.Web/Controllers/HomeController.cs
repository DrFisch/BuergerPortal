using BuergerPortal.Web.Features.Home.ViewModels;
using BuergerPortal.Web.Features.Termine.Contracts;
using BuergerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

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

            WeatherVm? weather = null;

            
            try
            {
                var weatherClient = _cf.CreateClient(); 
                var weatherRes = await weatherClient.GetFromJsonAsync<JsonElement>(
                    "https://api.open-meteo.com/v1/forecast?latitude=50.31&longitude=11.91&current_weather=true", ct);

                var current = weatherRes.GetProperty("current_weather");
                var temp = current.GetProperty("temperature").GetDouble();
                var code = current.GetProperty("weathercode").GetInt32();

                weather = new WeatherVm
                {
                    Temperature = temp,
                    Condition = GetWeatherDescription(code),
                    IconClass = GetWeatherIcon(code)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Wetterdaten.");
            }

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
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Index: request for appointments was cancelled by the client.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Laden der nächsten Termine (Index).");
                }
            }

            var vm = new HomeIndexVm
            {
                NextAppointment = nextAppointment,
                Weather = weather
            };

            return View(vm);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Standorte()
        {
            return View();
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Stadtplan()
        {
            return View();
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult News()
        {
 
            return View();
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

        // Hilfsmethodne für wetter
        private string GetWeatherDescription(int code) => code switch
        {
            0 => "Sonnig",
            1 or 2 or 3 => "Leicht bewölkt",
            45 or 48 => "Nebel",
            51 or 53 or 55 => "Nieselregen",
            61 or 63 or 65 => "Regen",
            71 or 73 or 75 => "Schneefall",
            95 => "Gewitter",
            _ => "Heiter"
        };

        private string GetWeatherIcon(int code) => code switch
        {
            0 => "bi-sun",
            1 or 2 or 3 => "bi-cloud-sun",
            45 or 48 => "bi-cloud-fog",
            >= 51 and <= 65 => "bi-cloud-rain",
            >= 71 and <= 75 => "bi-cloud-snow",
            95 => "bi-cloud-lightning",
            _ => "bi-cloud"
        };
    }
}
