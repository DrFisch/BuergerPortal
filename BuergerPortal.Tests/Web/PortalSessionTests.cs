using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;

namespace BuergerPortal.Tests.Web
{
    /// <summary>Regeln der Portal-Sitzung: Erneuern des Access-Tokens und Sitzungsende.</summary>
    public class PortalSessionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Token_lange_gueltig_bleibt_unveraendert() =>
            Assert.Equal(PortalSession.Outcome.Valid, PortalSession.Evaluate(Now, Now.AddHours(-1), Now.AddMinutes(30)));

        [Fact]
        public void Token_kurz_vor_Ablauf_wird_erneuert() =>
            Assert.Equal(PortalSession.Outcome.Refresh, PortalSession.Evaluate(Now, Now.AddHours(-1), Now.AddMinutes(4)));

        [Fact]
        public void Abgelaufenes_Token_beendet_die_Sitzung_wenn_keine_Erneuerung_klappt() =>
            Assert.Equal(PortalSession.Outcome.Expired, PortalSession.Evaluate(Now, Now.AddHours(-1), Now.AddSeconds(-1)));

        [Fact]
        public void Ohne_gespeicherten_Ablauf_gilt_die_Sitzung() =>
            Assert.Equal(PortalSession.Outcome.Valid, PortalSession.Evaluate(Now, null, null));

        [Fact]
        public void Nach_acht_Stunden_endet_die_Sitzung_auch_mit_gueltigem_Token() =>
            Assert.Equal(PortalSession.Outcome.Expired,
                PortalSession.Evaluate(Now, Now.AddHours(-8), Now.AddMinutes(50)));

        [Fact]
        public void Restzeit_ist_die_Inaktivitaetsgrenze_ausser_kurz_vor_der_Hoechstdauer()
        {
            Assert.Equal(TimeSpan.FromMinutes(30), PortalSession.Remaining(Now, Now.AddHours(-1)));
            Assert.Equal(TimeSpan.FromMinutes(10), PortalSession.Remaining(Now, Now.AddHours(-8).AddMinutes(10)));
            Assert.Equal(TimeSpan.Zero, PortalSession.Remaining(Now, Now.AddHours(-9)));
        }

        [Fact]
        public void Start_setzt_Sitzungscookie_mit_Ablauf_und_Anmeldezeit()
        {
            var props = new AuthenticationProperties { IsPersistent = true };
            PortalSession.Start(props, Now);

            Assert.False(props.IsPersistent);
            Assert.Equal(Now.AddMinutes(30), props.ExpiresUtc);
            Assert.Equal(Now, PortalSession.LoginTime(props));
        }

        [Fact]
        public void Liest_expires_at_aus_den_Sitzungsdaten()
        {
            var props = new AuthenticationProperties();
            props.StoreTokens([new AuthenticationToken { Name = "expires_at", Value = "2026-10-07T12:05:00.0000000+00:00" }]);

            Assert.Equal(Now.AddMinutes(5), AccessTokenRefresher.ExpiresAt(props));
            Assert.Null(AccessTokenRefresher.ExpiresAt(new AuthenticationProperties()));
        }
    }
}
