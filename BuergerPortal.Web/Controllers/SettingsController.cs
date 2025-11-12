using BuergerPortal.Web.Features.Settings.Contracts;
using BuergerPortal.Web.Features.Settings.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Net;

namespace BuergerPortal.Web.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public SettingsController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var http = _httpClientFactory.CreateClient("BuergerPortalApi");

            // Pfad ggf. anpassen: "api/users/me/settings"
            var dto = await TryGetSettingsOrDefault(http, "api/users/me/settings", ct);

            var vm = new SettingsVm
            {
                Theme = string.Equals(dto.Theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light",
                Language = string.IsNullOrWhiteSpace(dto.Language) ? "de" : dto.Language,
                PushEnabled = dto.PushEnabled,
                ReduceDataUsage = dto.ReduceDataUsage,
                AnalyticsOptIn = dto.AnalyticsOptIn,
                AllowGeolocation = dto.AllowGeolocation,
                Languages = new[]
                {
            new SelectListItem("Deutsch","de", dto.Language=="de"),
            new SelectListItem("English","en", dto.Language=="en")
        }
            };
            // nach dem Erstellen von vm:
            if (Request.Cookies.TryGetValue("theme", out var cookieTheme))
            {
                vm.Theme = string.Equals(cookieTheme, "dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            }


            if (dto.__ApiFallbackUsed) 
            { 
                TempData["Saved"] = "Hinweis: Einstellungen lokal mit Standardwerten geladen (API nicht erreichbar)."; 
            }

            return View("Index", vm);
        }

        private sealed record SafeUserSettingsDto(
            string Theme = "Light",
            string Language = "de",
            bool PushEnabled = false,
            bool ReduceDataUsage = false,
            bool AnalyticsOptIn = false,
            bool AllowGeolocation = false,
            bool __ApiFallbackUsed = false // Marker nur für UI
        );

        private async Task<SafeUserSettingsDto> TryGetSettingsOrDefault(HttpClient http, string path, CancellationToken ct)
        {
            try
            {
                var res = await http.GetAsync(path, ct);

                if (!res.IsSuccessStatusCode)
                    return new SafeUserSettingsDto(__ApiFallbackUsed: true);

                var dto = await res.Content.ReadFromJsonAsync<UserSettingsDto>(cancellationToken: ct);
                if (dto is null)
                    return new SafeUserSettingsDto(__ApiFallbackUsed: true);

                return new SafeUserSettingsDto(
                    Theme: dto.Theme ?? "Light",
                    Language: string.IsNullOrWhiteSpace(dto.Language) ? "de" : dto.Language,
                    PushEnabled: dto.PushEnabled,
                    ReduceDataUsage: dto.ReduceDataUsage,
                    AnalyticsOptIn: dto.AnalyticsOptIn,
                    AllowGeolocation: dto.AllowGeolocation,
                    __ApiFallbackUsed: false
                );
            }
            catch (HttpRequestException)
            {
                return new SafeUserSettingsDto(__ApiFallbackUsed: true);
            }
            catch (TaskCanceledException)
            {
                return new SafeUserSettingsDto(__ApiFallbackUsed: true);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SettingsVm vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                vm.Languages = new[]
                {
                new SelectListItem("Deutsch","de", vm.Language=="de"),
                new SelectListItem("English","en", vm.Language=="en")
            };
                return View("Index", vm);
            }

            var payload = new UserSettingsUpdateRequest
            {
                Theme = string.Equals(vm.Theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light",
                Language = string.IsNullOrWhiteSpace(vm.Language) ? "de" : vm.Language,
                PushEnabled = vm.PushEnabled,
                ReduceDataUsage = vm.ReduceDataUsage,
                AnalyticsOptIn = vm.AnalyticsOptIn,
                AllowGeolocation = vm.AllowGeolocation
            };

            var http = _httpClientFactory.CreateClient("BuergerPortalApi");
            var res = await http.PutAsJsonAsync("users/me/settings", payload, ct);

            if (res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.NoContent)
            {
                TempData["Saved"] = "Einstellungen gespeichert.";
                SetClientCookies(payload.Theme, payload.Language);
                return RedirectToAction(nameof(Index));
            }

            ProblemDetails? problem = null;
            try { problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct); } catch { }
            ModelState.AddModelError(string.Empty, problem?.Title ?? $"Fehler {(int)res.StatusCode} {res.ReasonPhrase}");
            if (!string.IsNullOrWhiteSpace(problem?.Detail)) ModelState.AddModelError(string.Empty, problem!.Detail);

            vm.Languages = new[]
            {
            new SelectListItem("Deutsch","de", vm.Language=="de"),
            new SelectListItem("English","en", vm.Language=="en")
        };
            return View("Index", vm);
        }

        private void SetClientCookies(string? theme, string? lang)
        {
            // 🔹 DEINE ZEILE
            var t = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
            Response.Cookies.Append("theme", t,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true
                });

            var l = string.IsNullOrWhiteSpace(lang) ? "de" : lang!;
            var cultureCookie = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(l));
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cultureCookie,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true
                });
        }
    }
}
