using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;

namespace AuthenticationServer.BundId
{
    /// <summary>
    /// Liefert die SAML-Konfiguration des Auth-Servers als Service Provider der BundID.
    /// Issuer, SSO-Adresse und Signaturzertifikat der BundID werden aus deren Metadaten gelesen –
    /// beim ersten Bedarf, danach zwischengespeichert. Ist die BundID beim Start noch nicht erreichbar,
    /// wird es beim nächsten Login erneut versucht.
    /// </summary>
    public sealed class BundIdSamlConfigurationProvider(IOptions<BundIdOptions> options,
        IHttpClientFactory httpClientFactory, BundIdTokenReplayCache replayCache,
        ILogger<BundIdSamlConfigurationProvider> logger)
    {
        private readonly SemaphoreSlim loadLock = new(1, 1);
        private Saml2Configuration? configuration;

        public async Task<Saml2Configuration> GetConfigurationAsync(CancellationToken ct = default)
        {
            if (configuration != null)
            {
                return configuration;
            }

            await loadLock.WaitAsync(ct);
            try
            {
                configuration ??= await LoadAsync();
                return configuration;
            }
            finally
            {
                loadLock.Release();
            }
        }

        private async Task<Saml2Configuration> LoadAsync()
        {
            var bundId = options.Value;
            var entityDescriptor = new EntityDescriptor();
            await entityDescriptor.ReadIdPSsoDescriptorFromUrlAsync(httpClientFactory, new Uri(bundId.IdpMetadataUrl));
            if (entityDescriptor.IdPSsoDescriptor == null)
            {
                throw new InvalidOperationException($"Keine IdP-Metadaten unter {bundId.IdpMetadataUrl}.");
            }

            var config = CreateConfiguration(bundId.SpEntityId, entityDescriptor.EntityId,
                entityDescriptor.IdPSsoDescriptor.SingleSignOnServices.First().Location,
                entityDescriptor.IdPSsoDescriptor.SigningCertificates, replayCache);
            logger.LogInformation("BundID-Metadaten geladen: IdP {EntityId}, SSO {SsoUrl}",
                config.AllowedIssuer, config.SingleSignOnDestination);
            return config;
        }

        /// <summary>
        /// Baut die Prüfkonfiguration des Service Providers aus den Angaben der IdP-Metadaten.
        /// Öffentlich, damit Tests genau dieselben Sicherheitseinstellungen verwenden.
        /// </summary>
        public static Saml2Configuration CreateConfiguration(string spEntityId, string idpEntityId,
            Uri singleSignOnDestination, IEnumerable<X509Certificate2> idpSigningCertificates, ITokenReplayCache replayCache)
        {
            var config = new Saml2Configuration
            {
                Issuer = spEntityId,
                // Nur Antworten mit diesem Issuer und einer Signatur mit diesen Zertifikaten werden akzeptiert.
                AllowedIssuer = idpEntityId,
                SingleSignOnDestination = singleSignOnDestination,
                // Das Zertifikat der BundID (hier: des Simulators) ist selbstsigniert. Vertraut wird genau dem
                // Zertifikat aus den Metadaten; eine Prüfung der Zertifikatskette ist daher nicht möglich.
                CertificateValidationMode = X509CertificateValidationMode.None,
                RevocationMode = X509RevocationMode.NoCheck,
                // Replay-Schutz: bei ITfoxtec standardmäßig aus und nur mit gesetztem Cache wirksam.
                DetectReplayedTokens = true,
                TokenReplayCache = replayCache,
            };
            // Die Assertion muss an genau diesen Service Provider gerichtet sein (Audience).
            config.AllowedAudienceUris.Add(spEntityId);
            config.SignatureValidationCertificates.AddRange(idpSigningCertificates);
            if (config.SignatureValidationCertificates.Count == 0)
            {
                throw new InvalidOperationException("Die IdP-Metadaten enthalten kein Signaturzertifikat.");
            }
            return config;
        }
    }
}
