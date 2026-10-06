using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BuergerPortal.BundId
{
    /// <summary>Was sich der Auth-Server zwischen AuthnRequest und Response merken muss.</summary>
    public sealed record BundIdLoginState(string RequestId, int RequestedLevel, string ReturnUrl);

    /// <summary>
    /// Speichert den Anmeldezustand in einem verschlüsselten, zeitlich begrenzten Cookie.
    /// SameSite=None + Secure, weil die BundID die Response per Cross-Site-POST an den ACS schickt
    /// (bei SameSite=Lax würde der Browser das Cookie dabei nicht mitsenden). Pfad: Basispfad der Anwendung + "/bundid",
    /// damit das Cookie auch unter einem PathBase (z. B. Postfach unter /postfach) beim ACS ankommt.
    /// </summary>
    public sealed class BundIdLoginStateStore(IDataProtectionProvider dataProtectionProvider)
    {
        private const string CookieName = "bundid_login";
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private readonly ITimeLimitedDataProtector protector =
            dataProtectionProvider.CreateProtector("BundId.LoginState").ToTimeLimitedDataProtector();

        public void Save(HttpResponse response, BundIdLoginState state)
        {
            var value = protector.Protect(JsonSerializer.Serialize(state), Lifetime);
            response.Cookies.Append(CookieName, value,
                CreateCookieOptions(response.HttpContext.Request, DateTimeOffset.UtcNow.Add(Lifetime)));
        }

        // Liefert null, wenn das Cookie fehlt, manipuliert oder abgelaufen ist.
        public BundIdLoginState? Read(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<BundIdLoginState>(protector.Unprotect(value));
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
            {
                return null;
            }
        }

        public void Delete(HttpResponse response)
        {
            response.Cookies.Delete(CookieName, CreateCookieOptions(response.HttpContext.Request, null));
        }

        private static CookieOptions CreateCookieOptions(HttpRequest request, DateTimeOffset? expires) => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = request.PathBase.Add(new PathString("/bundid")).Value,
            Expires = expires,
            IsEssential = true,
        };
    }
}
