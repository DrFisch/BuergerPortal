using BuergerPortal.Api.Extensions;
using BuergerPortal.Api.Push;
using BuergerPortal.Application.Appointments.Erinnerung;
using BuergerPortal.Application.Interfaces.Postkorb;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Api.Postkorb
{
    /// <summary>Einstellungen der Erinnerung vor dem Termin (Abschnitt "TerminErinnerung").</summary>
    public sealed class TerminErinnerungOptions
    {
        public const string Section = "TerminErinnerung";

        public bool Enabled { get; set; } = true;
        // So viele Stunden vor Beginn wird erinnert (nur Termine, die vorher gebucht wurden).
        public int StundenVorher { get; set; } = 24;
        public int IntervallMinuten { get; set; } = 5;
    }

    /// <summary>
    /// Erinnert vor dem Termin: Nachricht ins BundID-Postfach und – wenn zugestellt und abonniert – eine Benachrichtigung
    /// aufs Handy. Läuft regelmäßig im Hintergrund; jede Erinnerung höchstens einmal.
    /// </summary>
    public sealed class TerminErinnerungDienst(IServiceScopeFactory scopes, IOptions<TerminErinnerungOptions> options,
        PushQueue pushQueue, ILogger<TerminErinnerungDienst> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var settings = options.Value;
            if (!settings.Enabled)
            {
                logger.LogInformation("Terminerinnerung ist ausgeschaltet");
                return;
            }
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, settings.IntervallMinuten)));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Terminerinnerung fehlgeschlagen");
                }
            }
        }

        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            await using var scope = scopes.CreateAsyncScope();
            var erinnerungen = await scope.ServiceProvider.GetRequiredService<ITerminErinnerungService>()
                .FaelligeAsync(TimeSpan.FromHours(options.Value.StundenVorher), ct);
            var postkorb = scope.ServiceProvider.GetRequiredService<IPostkorbService>();
            foreach (var e in erinnerungen)
            {
                var zustellung = await postkorb.SendAsync(Nachricht(e), ct);
                logger.LogInformation("Terminerinnerung {TerminId}: {Zustellung}", e.TerminId, zustellung);
                if (zustellung == PostkorbDeliveryStatus.Delivered)
                {
                    pushQueue.TryEnqueue(e.UserId, Benachrichtigung(e));
                }
            }
            return erinnerungen.Count;
        }

        public static PostkorbMessage Nachricht(TerminErinnerung e) => new(e.PostkorbHandle,
            $"Erinnerung an Ihren Termin: {e.Service.GetDisplayName()}",
            $"""
            Guten Tag,

            wir erinnern Sie an Ihren Termin.

            Leistung: {e.Service.GetDisplayName()}
            Ort: {e.Location.GetDisplayName()}
            Termin: {PostkorbBenachrichtigung.Ortszeit(e.StartUtc)}

            Falls Sie den Termin nicht wahrnehmen können, stornieren Sie ihn bitte im BürgerPortal in der Terminübersicht – dann wird er für andere frei.
            In den Termindetails finden Sie auch den QR-Code zum Einchecken.

            Diese Nachricht wurde automatisch vom BürgerPortal erstellt.
            """.ReplaceLineEndings("\n"),
            "Terminvereinbarung");

        // Sperrbildschirm: nur Zeit und Ort, keine Namen; ein Tipp öffnet den Termin.
        public static PushNotification Benachrichtigung(TerminErinnerung e) => new(
            $"Erinnerung: Termin {PostkorbBenachrichtigung.Ortszeit(e.StartUtc)}",
            $"{e.Service.GetDisplayName()} · {e.Location.GetDisplayName()}",
            $"/Termine/Details/{e.TerminId}",
            $"termin-{e.TerminId}");
    }
}
