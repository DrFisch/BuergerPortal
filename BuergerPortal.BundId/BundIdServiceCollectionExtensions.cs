using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuergerPortal.BundId
{
    public static class BundIdServiceCollectionExtensions
    {
        /// <summary>
        /// Registriert alles, was ein Service Provider der BundID braucht: Einstellungen (Abschnitt "BundId"),
        /// Metadaten-Abruf, Anmeldezustand, Replay-Schutz und den SAML-Ablauf (<see cref="BundIdSamlService"/>).
        /// </summary>
        public static IServiceCollection AddBundIdServiceProvider(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BundIdOptions>(configuration.GetSection(BundIdOptions.SectionName));
            services.AddHttpClient();
            services.AddDataProtection();
            services.AddSingleton<BundIdTokenReplayCache>();
            services.AddSingleton<BundIdSamlConfigurationProvider>();
            services.AddSingleton<BundIdLoginStateStore>();
            services.AddSingleton<BundIdSamlService>();
            return services;
        }
    }
}
