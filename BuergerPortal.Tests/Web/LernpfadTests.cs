using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using BuergerPortal.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BuergerPortal.Tests.Web
{
    /// <summary>
    /// Lernpfad „Schritt für Schritt“ im Portal (Services/Lernpfad.cs): Umleitung zur Erklärseite von Schritt 1,
    /// Merken des Code-Tauschs für Schritt 5 – ohne dass ein Token auf einer Seite landet.
    /// </summary>
    public class LernpfadTests
    {
        private static readonly AuthenticationScheme Scheme =
            new(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));

        [Fact]
        public void Ohne_Cookie_kein_Lernpfad()
        {
            var context = new DefaultHttpContext();
            Assert.False(Lernpfad.IsRequested(context.Request));
            context.Request.Headers.Cookie = $"{Lernpfad.CookieName}=1";
            Assert.True(Lernpfad.IsRequested(context.Request));
        }

        [Fact]
        public void Schritt1_leitet_die_OIDC_Anfrage_zur_Erklaerseite_und_verbraucht_das_Cookie()
        {
            var context = new DefaultHttpContext();
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("portal.example.test");
            var properties = new AuthenticationProperties { RedirectUri = "/Termine" };
            var redirect = new RedirectContext(context, Scheme, new OpenIdConnectOptions(), properties)
            {
                ProtocolMessage = new OpenIdConnectMessage { IssuerAddress = "https://auth.example.test/connect/authorize" },
            };

            Lernpfad.ZurErklaerung(redirect);

            Assert.Equal("https://portal.example.test/Lernpfad/Anfrage", redirect.ProtocolMessage.IssuerAddress);
            Assert.Equal("1", redirect.ProtocolMessage.GetParameter(Lernpfad.Parameter));
            Assert.Equal("1", properties.Items[Lernpfad.PropertyKey]);
            Assert.Contains($"{Lernpfad.CookieName}=;", context.Response.Headers.SetCookie.ToString());
        }

        [Theory]
        [InlineData("state", "CfDJ8HO0wPOiAbcdefghijklmnopqrstuvwxyz", "CfDJ8HO0wPOi…")]
        [InlineData("nonce", "639278662618123456789012345", "639278662618…")]
        [InlineData("code_challenge", "8kJCm-OvvsLxAbcdefghijklmnop", "8kJCm-OvvsLx…")]
        [InlineData("client_id", "mvc_web_mit_langem_namen_123456", "mvc_web_mit_langem_namen_123456")]
        [InlineData("nonce", "kurz", "kurz")]
        public void Zufallswerte_werden_gekuerzt_alles_andere_nicht(string name, string wert, string erwartet) =>
            Assert.Equal(erwartet, Lernpfad.Kuerzen(name, wert));

        [Fact]
        public void Schritt5_merkt_den_Code_Tausch_ohne_Tokens_und_fuehrt_zur_Erklaerseite()
        {
            var (validated, properties) = TokenValidated(lernpfad: true);

            Lernpfad.TokensMerken(validated);

            Assert.Equal("/Lernpfad/Angekommen?ziel=%2FTermine", properties.RedirectUri);
            Assert.False(properties.Items.ContainsKey(Lernpfad.PropertyKey));
            var json = properties.Items[Lernpfad.InfoKey]!;
            var info = JsonSerializer.Deserialize<LernpfadTokenInfo>(json)!;
            Assert.Equal(["id_token", "access_token", "refresh_token"], info.Tokens);
            Assert.Equal("RS256", info.Algorithmus);
            Assert.Equal("https://auth.example.test/", info.Aussteller);
            Assert.Equal("mvc_web", info.Zielgruppe);
            Assert.Equal("STORK-QAA-Level-3", info.Niveau);
            Assert.Equal("Elster", info.Mittel);
            Assert.True(info.NonceGeprueft);
            Assert.Equal(3600, info.AccessTokenSekunden);
            // Keine Tokens in der gemerkten Zusammenfassung
            Assert.DoesNotContain("geheimes-access-token", json);
            Assert.DoesNotContain("geheimes-refresh-token", json);
            Assert.DoesNotContain("eyJ", json);
        }

        [Fact]
        public void Ohne_Lernpfad_bleibt_das_Ziel_der_Anmeldung()
        {
            var (validated, properties) = TokenValidated(lernpfad: false);

            Lernpfad.TokensMerken(validated);

            Assert.Equal("/Termine", properties.RedirectUri);
            Assert.False(properties.Items.ContainsKey(Lernpfad.InfoKey));
        }

        private static (TokenValidatedContext, AuthenticationProperties) TokenValidated(bool lernpfad)
        {
            var properties = new AuthenticationProperties { RedirectUri = "/Termine" };
            if (lernpfad)
            {
                properties.Items[Lernpfad.PropertyKey] = "1";
            }
            var key = new SymmetricSecurityKey(new byte[32]) { KeyId = "test-schluessel-0001" };
            var idToken = new JwtSecurityToken(
                new JwtHeader(new SigningCredentials(key, "RS256")),
                new JwtPayload("https://auth.example.test/", "mvc_web",
                    [new Claim("acr", "STORK-QAA-Level-3"), new Claim("amr", "Elster"), new Claim("nonce", "n-123")],
                    DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(20), DateTime.UtcNow.AddMinutes(-1)));
            var context = new TokenValidatedContext(new DefaultHttpContext(), Scheme, new OpenIdConnectOptions(),
                new ClaimsPrincipal(new ClaimsIdentity("test")), properties)
            {
                SecurityToken = idToken,
                TokenEndpointResponse = new OpenIdConnectMessage
                {
                    IdToken = "eyJhbGciOi.test.signatur",
                    AccessToken = "geheimes-access-token",
                    RefreshToken = "geheimes-refresh-token",
                    ExpiresIn = "3600",
                    Scope = "openid profile bundid",
                },
            };
            return (context, properties);
        }
    }
}
