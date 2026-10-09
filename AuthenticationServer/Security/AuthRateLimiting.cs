using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthenticationServer.Security;

/// <summary>
/// Begrenzt Anfragen an die Anmelde-Endpunkte je Client-IP und Minute (feste Zeitfenster). Schutz gegen das massenhafte
/// Einspielen von SAML-Responses an den ACS, gegen Durchprobieren von Client-Geheimnissen am Token-Endpunkt und gegen
/// Überlastung. Die Client-IP stammt hinter Caddy aus X-Forwarded-For (Caddy überschreibt den Wert des Browsers).
/// <para>
/// Grenzen (Annahme, für das Studienprojekt großzügig gewählt; hinter NAT teilen sich viele Personen eine IP), änderbar
/// unter <c>RateLimiting:*</c>:
/// <list type="bullet">
/// <item><c>Saml</c> (30): <c>/bundid/login</c> und <c>/bundid/acs</c> – eine Anmeldung braucht je einen Aufruf.</item>
/// <item><c>Oidc</c> (60): übrige <c>/connect/*</c> (authorize, logout) – je Anmeldung zwei bis drei Aufrufe.</item>
/// <item><c>Backchannel</c> (300): <c>/connect/token</c> und <c>/connect/userinfo</c> – ruft nur das Portal auf, von seiner
/// einen IP für alle Personen.</item>
/// </list>
/// </para>
/// </summary>
public static class AuthRateLimiting
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public sealed record Limits(int Saml = 30, int Oidc = 60, int Backchannel = 300);

    /// <summary>Bereich und Grenze für einen Pfad; <c>null</c> = nicht begrenzt (Seiten, Metadaten, statische Dateien).</summary>
    public static (string Area, int PermitLimit)? For(PathString path, Limits limits) =>
        path.StartsWithSegments("/bundid/acs") || path.StartsWithSegments("/bundid/login") ? ("saml", limits.Saml)
        : path.StartsWithSegments("/connect/token") || path.StartsWithSegments("/connect/userinfo") ? ("backchannel", limits.Backchannel)
        : path.StartsWithSegments("/connect") ? ("oidc", limits.Oidc)
        : null;

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection("RateLimiting").Get<Limits>() ?? new Limits();
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (For(context.Request.Path, limits) is not { } limit)
                {
                    return RateLimitPartition.GetNoLimiter("frei");
                }
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unbekannt";
                return RateLimitPartition.GetFixedWindowLimiter($"{limit.Area}|{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit.PermitLimit,
                    Window = Window,
                    QueueLimit = 0,
                });
            });
            options.OnRejected = async (rejected, ct) =>
            {
                var http = rejected.HttpContext;
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthRateLimiting))
                    .LogWarning("Zu viele Anfragen an {Path} von {Ip} – abgelehnt (429)", http.Request.Path, http.Connection.RemoteIpAddress);
                http.Response.Headers.RetryAfter = ((int)Window.TotalSeconds).ToString();
                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync(
                    "Zu viele Anmeldeversuche in kurzer Zeit. Bitte warten Sie eine Minute und versuchen Sie es dann erneut.", ct);
            };
        });
    }
}
