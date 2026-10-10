using BuergerPortal.Application.Interfaces.Repositories;

namespace BuergerPortal.Api.Push
{
    /// <summary>Benachrichtigt alle Geräte einer Person; Abos, die der Push-Dienst nicht mehr kennt, werden gelöscht.</summary>
    public sealed class PushNotifier(IPushSubscriptionRepository subscriptions, WebPushSender sender)
    {
        /// <returns>Anzahl der Geräte, an die der Push-Dienst die Nachricht angenommen hat.</returns>
        public async Task<int> NotifyAsync(Guid userId, PushNotification notification, CancellationToken ct)
        {
            var sent = 0;
            foreach (var subscription in await subscriptions.GetByUserIdAsync(userId, ct))
            {
                switch (await sender.SendAsync(subscription, notification, ct))
                {
                    case PushSendResult.Sent:
                        sent++;
                        break;
                    case PushSendResult.Gone:
                        await subscriptions.DeleteByEndpointAsync(subscription.Endpoint, ct);
                        break;
                }
            }
            return sent;
        }
    }
}
