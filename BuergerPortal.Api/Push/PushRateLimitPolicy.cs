using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace BuergerPortal.Api.Push
{
    /// <summary>
    /// Abos anlegen und Test-Benachrichtigungen: höchstens 10 je Minute und Person (Claim <c>sub</c>). Jede
    /// Test-Benachrichtigung löst Anfragen an fremde Push-Dienste aus – die API soll dafür nicht missbraucht werden.
    /// </summary>
    public sealed class PushRateLimitPolicy : IRateLimiterPolicy<string>
    {
        public const string Name = "push";
        public const int PermitLimit = 10;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        public const string RejectedDetail =
            "Sie haben in kurzer Zeit sehr oft Benachrichtigungen eingerichtet oder getestet. Bitte warten Sie eine Minute.";

        public RateLimitPartition<string> GetPartition(HttpContext httpContext) => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("sub")?.Value ?? "ip:" + httpContext.Connection.RemoteIpAddress,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitLimit, Window = Window, QueueLimit = 0 });

        // ersetzt für diese Endpunkte die allgemeine Meldung (die spricht von Terminbuchungen)
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = async (rejected, ct) =>
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
    }
}
