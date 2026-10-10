using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuergerPortal.Api.Push
{
    public static class PushServiceCollectionExtensions
    {
        /// <summary>Benachrichtigungen per Web Push (Abschnitt "Push"); ohne VAPID-Schlüssel bleibt alles aus.</summary>
        public static IServiceCollection AddWebPush(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PushOptions>(configuration.GetSection(PushOptions.Section));
            services.TryAddSingleton(TimeProvider.System);
            services.AddHttpClient<WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15))
                // keine Weiterleitungen folgen: Ziel bleibt der geprüfte Push-Dienst (PushOptions.IsAllowedEndpoint)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
            services.AddScoped<PushNotifier>();
            services.AddRateLimiter(o => o.AddPolicy<string, PushRateLimitPolicy>(PushRateLimitPolicy.Name));
            return services;
        }
    }
}
