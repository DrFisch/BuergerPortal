using BuergerPortal.Api.Push;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Application.Interfaces.Postkorb;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Api.Antraege
{
    /// <summary>Einstellungen der simulierten Antragsbearbeitung (Abschnitt "AntragSimulation").</summary>
    public sealed class AntragSimulationOptions
    {
        public const string Section = "AntragSimulation";

        // Standardmäßig an – ohne Behördensicht bliebe jeder Antrag sonst "eingereicht".
        public bool Enabled { get; set; } = true;
        public int InBearbeitungNachMinuten { get; set; } = 2;
        public int AbschlussNachMinuten { get; set; } = 5;
        public int IntervallSekunden { get; set; } = 30;
    }

    /// <summary>
    /// Simulierte Bearbeitung im Hintergrund: Regelmäßig schaltet sie fällige Anträge einen Schritt weiter
    /// (eingereicht → in Bearbeitung → abgeschlossen) und schickt je Wechsel eine Statusmeldung ins BundID-Postfach und –
    /// wenn zugestellt und abonniert – eine Benachrichtigung aufs Handy.
    /// </summary>
    public sealed class AntragBearbeitungSimulation(IServiceScopeFactory scopes, IOptions<AntragSimulationOptions> options,
        PushQueue pushQueue, ILogger<AntragBearbeitungSimulation> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var settings = options.Value;
            if (!settings.Enabled)
            {
                logger.LogInformation("Simulierte Antragsbearbeitung ist ausgeschaltet");
                return;
            }
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, settings.IntervallSekunden)));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // z. B. Datenbank kurz nicht erreichbar – beim nächsten Durchlauf erneut
                    logger.LogWarning(ex, "Simulierte Antragsbearbeitung fehlgeschlagen");
                }
            }
        }

        /// <summary>Ein Durchlauf; liefert die Zahl der Statuswechsel.</summary>
        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            var settings = options.Value;
            await using var scope = scopes.CreateAsyncScope();
            var status = scope.ServiceProvider.GetRequiredService<IAntragStatusService>();
            var postkorb = scope.ServiceProvider.GetRequiredService<IPostkorbService>();

            var wechsel = await status.BearbeitungSimulierenAsync(TimeSpan.FromMinutes(settings.InBearbeitungNachMinuten),
                TimeSpan.FromMinutes(settings.AbschlussNachMinuten), ct);
            foreach (var w in wechsel)
            {
                var zustellung = await postkorb.SendAsync(AntragStatusmeldung.Nachricht(w), ct);
                logger.LogInformation("Antrag {AntragId}: {Status}, Statusmeldung {Zustellung}", w.AntragId, w.Status, zustellung);
                if (zustellung == PostkorbDeliveryStatus.Delivered)
                {
                    pushQueue.TryEnqueue(w.ApplicantUserId, AntragStatusmeldung.Benachrichtigung(w));
                }
            }
            return wechsel.Count;
        }
    }
}
