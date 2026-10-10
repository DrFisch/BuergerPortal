using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace BuergerPortal.Api.Push
{
    public sealed record PushJob(Guid UserId, PushNotification Notification);

    /// <summary>
    /// Warteschlange für Benachrichtigungen: Die Anfrage (z. B. eine Buchung) reiht nur ein und antwortet sofort; den
    /// Versand an die Push-Dienste übernimmt <see cref="PushQueueWorker"/> im Hintergrund. Nach einem Neustart der API
    /// sind noch nicht versendete Benachrichtigungen verloren – die Nachricht selbst liegt sicher im Postfach.
    /// </summary>
    public sealed class PushQueue(IOptions<PushOptions> options, ILogger<PushQueue> logger)
    {
        public const int Capacity = 500;

        private readonly Channel<PushJob> channel = Channel.CreateBounded<PushJob>(
            new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

        public ChannelReader<PushJob> Reader => channel.Reader;

        /// <returns>false, wenn Push nicht eingerichtet ist oder die Warteschlange voll ist.</returns>
        public bool TryEnqueue(Guid userId, PushNotification notification)
        {
            if (!options.Value.IsConfigured) return false;
            if (channel.Writer.TryWrite(new PushJob(userId, notification))) return true;
            logger.LogWarning("Push-Warteschlange voll ({Capacity}) – Benachrichtigung verworfen", Capacity);
            return false;
        }
    }

    public sealed class PushQueueWorker(PushQueue queue, IServiceScopeFactory scopes, ILogger<PushQueueWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<PushNotifier>().NotifyAsync(job.UserId, job.Notification, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // z. B. Datenbank kurz nicht erreichbar – die nächste Benachrichtigung soll trotzdem rausgehen
                    logger.LogWarning(ex, "Benachrichtigung konnte nicht versendet werden");
                }
            }
        }
    }
}
