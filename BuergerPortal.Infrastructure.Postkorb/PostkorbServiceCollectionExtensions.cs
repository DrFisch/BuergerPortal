using BuergerPortal.Application.Interfaces.Postkorb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuergerPortal.Infrastructure.Postkorb
{
    public static class PostkorbServiceCollectionExtensions
    {
        /// <summary>
        /// Registriert <see cref="IPostkorbService"/> mit typisiertem HttpClient. Die Einstellungen (Abschnitt
        /// "Postkorb") werden beim Start geprüft: fehlt Adresse oder Schlüssel, startet die Anwendung nicht.
        /// </summary>
        public static IServiceCollection AddPostkorbService(this IServiceCollection services, IConfiguration cfg)
        {
            services.AddOptions<PostkorbOptions>()
                .Bind(cfg.GetSection(PostkorbOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<IPostkorbService, HttpPostkorbService>((sp, client) =>
            {
                var o = sp.GetRequiredService<IOptions<PostkorbOptions>>().Value;
                client.BaseAddress = new Uri(o.BaseUrl.EndsWith('/') ? o.BaseUrl : o.BaseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                client.DefaultRequestHeaders.Add(HttpPostkorbService.ApiKeyHeader, o.ApiKey);
            });
            return services;
        }
    }
}
