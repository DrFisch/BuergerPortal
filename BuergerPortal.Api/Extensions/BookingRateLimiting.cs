using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BuergerPortal.Api.Extensions
{
    /// <summary>
    /// Begrenzt Buchen, Stornieren, Löschen und Standortwechsel von Terminen je angemeldeter Person (Claim <c>sub</c>):
    /// höchstens <c>RateLimiting:BuchungenProMinute</c> (Standard 10) Änderungen je Minute. Schützt die Terminvergabe davor,
    /// dass ein Konto per Skript alle freien Zeiten belegt oder ständig umbucht. Darüber gibt es 429 mit einer
    /// verständlichen Meldung (ProblemDetails – das Portal zeigt <c>detail</c> an).
    /// </summary>
    public static class BookingRateLimiting
    {
        public const string Policy = "buchungen";
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        public const string RejectedDetail =
            "Sie haben in kurzer Zeit sehr viele Termine gebucht oder geändert. Bitte warten Sie eine Minute.";

        public static IServiceCollection AddBookingRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddRateLimiter(options =>
            {
                // erst beim Aufbau der Optionen lesen: dann ist die Konfiguration vollständig (auch in Tests)
                var permitLimit = configuration.GetValue("RateLimiting:BuchungenProMinute", 10);
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst("sub")?.Value ?? "ip:" + context.Connection.RemoteIpAddress,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = Window, QueueLimit = 0 }));
                options.OnRejected = async (rejected, ct) =>
                {
                    var http = rejected.HttpContext;
                    http.Response.Headers.RetryAfter = ((int)Window.TotalSeconds).ToString();
                    await http.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Zu viele Anfragen",
                        Detail = RejectedDetail,
                    }, options: null, contentType: "application/problem+json", cancellationToken: ct);
                };
            });
        }
    }
}
