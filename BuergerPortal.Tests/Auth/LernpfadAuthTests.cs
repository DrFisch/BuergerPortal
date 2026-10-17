using AuthenticationServer.BundId;

namespace BuergerPortal.Tests.Auth
{
    /// <summary>
    /// Lernpfad „Schritt für Schritt“ im Auth-Server: Nur eine OIDC-Anfrage mit bpsim_lernpfad=1 hält bei Schritt 2 und
    /// 4 an; der Merker steckt in der Rücksprungadresse des Anmeldezustands.
    /// </summary>
    public class LernpfadAuthTests
    {
        [Theory]
        [InlineData("/connect/authorize?client_id=mvc_web&bpsim_lernpfad=1&scope=openid", true)]
        [InlineData("/connect/authorize?client_id=mvc_web&scope=openid", false)]
        [InlineData("/connect/authorize?bpsim_lernpfad=0", false)]
        [InlineData("/bundid/inspektor?bpsim_lernpfad=1", false)]     // nur OIDC-Anfragen
        [InlineData("/", false)]
        [InlineData(null, false)]
        public void Lernpfad_nur_fuer_OIDC_Anfragen_mit_Merker(string? returnUrl, bool erwartet) =>
            Assert.Equal(erwartet, Lernpfad.IsActive(returnUrl));
    }
}
