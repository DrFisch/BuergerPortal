using Microsoft.AspNetCore.Authentication;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Diagnose-Adressen für die Entwicklung: <c>/auth/debug</c> (Claims und dekodierte Inhalte von ID- und Access-Token
/// der eigenen Sitzung) und <c>/home/testuser</c> (Claims). Im Betrieb haben sie nichts zu suchen – Token-Inhalte und
/// BundID-Attribute gehören nicht auf eine öffentliche Seite. Deshalb nur eingeschaltet in der Umgebung Development
/// oder ausdrücklich mit <c>Diagnostics:Enabled=true</c>; sonst gibt es die Adressen nicht (404).
/// </summary>
public static class DiagnosticEndpoints
{
    public const string ConfigKey = "Diagnostics:Enabled";

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool?>(ConfigKey) ?? environment.IsDevelopment();

    public static void MapDiagnosticEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/debug", async (HttpContext ctx) =>
        {
            var idToken = await ctx.GetTokenAsync("id_token");
            var accessToken = await ctx.GetTokenAsync("access_token");
            var refreshToken = await ctx.GetTokenAsync("refresh_token");

            var result = new
            {
                Authenticated = ctx.User.Identity?.IsAuthenticated,
                Name = ctx.User.Identity?.Name,
                Claims = ctx.User.Claims.Select(c => new { c.Type, c.Value }),
                Tokens = new
                {
                    HasIdToken = idToken != null,
                    HasAccessToken = accessToken != null,
                    HasRefreshToken = refreshToken != null
                },
                IdTokenPayload = DecodeJwtPayload(idToken),
                AccessTokenPayload = DecodeJwtPayload(accessToken)
            };

            await ctx.Response.WriteAsJsonAsync(result);
        }).RequireAuthorization();

        app.MapGet("/home/testuser", (HttpContext ctx) =>
            Results.Text(string.Join("\n", ctx.User.Claims.Select(c => $"{c.Type} = {c.Value}"))))
            .RequireAuthorization();
    }

    private static string? DecodeJwtPayload(string? jwt)
    {
        if (string.IsNullOrEmpty(jwt)) return null;
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;
        var s = parts[1].Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
