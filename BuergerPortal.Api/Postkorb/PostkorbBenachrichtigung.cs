using BuergerPortal.Api.Push;
using BuergerPortal.Application.Interfaces.Postkorb;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace BuergerPortal.Api.Postkorb
{
    /// <summary>
    /// Bestätigungen in das BundID-Postfach der angemeldeten Person. Das Ergebnis geht im Antwort-Header
    /// "X-Postkorb-Status" an das Portal, damit es bei fehlgeschlagener Zustellung einen Hinweis zeigen kann.
    /// </summary>
    public static class PostkorbBenachrichtigung
    {
        public const string StatusHeader = "X-Postkorb-Status";

        // Claim im Access-Token, vom Auth-Server aus der BundID-Anmeldung übernommen.
        public const string PostkorbHandleClaim = "postkorb_handle";

        public static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");
        private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        /// <summary>UTC-Zeitpunkt als deutsche Ortszeit, z. B. "Mittwoch, 07.10.2026, 10:00 Uhr".</summary>
        public static string Ortszeit(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Berlin)
                .ToString("dddd, dd.MM.yyyy, HH:mm 'Uhr'", Deutsch);

        /// <summary>Anrede mit dem Namen aus der BundID (Claim "name").</summary>
        public static string Anrede(this ControllerBase controller) =>
            string.IsNullOrWhiteSpace(controller.User.Identity?.Name)
                ? "Guten Tag,"
                : $"Guten Tag {controller.User.Identity.Name},";

        public static async Task<PostkorbDeliveryStatus> SendPostkorbAsync(this ControllerBase controller,
            IPostkorbService postkorb, string title, string content, string service, CancellationToken ct,
            int storkQaaLevel = 1)
        {
            var handle = controller.User.FindFirst(PostkorbHandleClaim)?.Value;
            // Rohstrings übernehmen die Zeilenenden der Quelldatei (CRLF) – einheitlich \n verschicken.
            var text = $"{content}\n\nDiese Nachricht wurde automatisch vom BürgerPortal erstellt.".ReplaceLineEndings("\n");
            var status = await postkorb.SendAsync(new PostkorbMessage(handle, title, text, service, storkQaaLevel), ct);

            controller.Response.Headers[StatusHeader] = status switch
            {
                PostkorbDeliveryStatus.Delivered => "zugestellt",
                PostkorbDeliveryStatus.NoMailbox => "kein-postfach",
                _ => "fehlgeschlagen",
            };

            // Benachrichtigung aufs Handy (Web Push), falls eingerichtet und abonniert: nur der Betreff, den Inhalt
            // liest die Person im Postfach. Im Hintergrund – die Antwort wartet nicht auf die Push-Dienste.
            if (status == PostkorbDeliveryStatus.Delivered && Guid.TryParse(controller.User.FindFirst("sub")?.Value, out var userId))
            {
                controller.HttpContext.RequestServices.GetService<PushQueue>()?.TryEnqueue(userId,
                    new PushNotification("Neue Nachricht im BundID-Postfach", title, "/Postfach", "postfach"));
            }
            return status;
        }
    }
}
