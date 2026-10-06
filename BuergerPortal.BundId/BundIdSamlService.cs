using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuergerPortal.BundId
{
    public enum BundIdLoginOutcome
    {
        // Assertion gültig, Niveau ausreichend.
        Success,
        // Abbruch bei der BundID (oder Fehlerstatus ohne Assertion).
        Cancelled,
        // Antwort nicht verwendbar: abgelaufen, ungültig, Niveau zu niedrig oder Pflichtangabe fehlt.
        Failed,
    }

    /// <summary>Ergebnis der Prüfung einer SAML-Response am Assertion Consumer Service.</summary>
    public sealed record BundIdLoginResult
    {
        public required BundIdLoginOutcome Outcome { get; init; }

        // Aus dem gemerkten Anmeldezustand – für "Erneut anmelden" und den Rücksprung.
        public required int RequestedLevel { get; init; }
        public required string ReturnUrl { get; init; }

        public BundIdAttributes? Attributes { get; init; }

        // Verständlicher Text für die Fehlerseite (nur bei Failed).
        public string ErrorTitle { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
    }

    /// <summary>Die Metadaten der BundID sind nicht abrufbar; eine Anmeldung ist gerade nicht möglich.</summary>
    public sealed class BundIdUnavailableException(Exception innerException)
        : Exception("BundID-Metadaten nicht abrufbar.", innerException)
    {
        public const string Title = "BundID nicht erreichbar";
        public const string UserMessage =
            "Die Anmeldung über die BundID ist gerade nicht möglich. Bitte versuchen Sie es später erneut.";
    }

    /// <summary>
    /// SAML-Ablauf eines Service Providers der BundID: AuthnRequest senden, Response am ACS prüfen, SP-Metadaten.
    /// Auth-Server und Postkorb-Simulation nutzen denselben Ablauf, damit die Sicherheitsprüfungen nur einmal existieren.
    /// </summary>
    public sealed class BundIdSamlService(IOptions<BundIdOptions> options,
        BundIdSamlConfigurationProvider configurationProvider, BundIdLoginStateStore loginStateStore,
        ILogger<BundIdSamlService> logger)
    {
        // Gefordertes Niveau übernehmen, wenn es ein BundID-Niveau ist (1, 3, 4), sonst das Standardniveau.
        public int NormalizeLevel(int? level) => level is 1 or 3 or 4 ? level.Value : options.Value.DefaultTrustLevel;

        /// <summary>
        /// Startet die Anmeldung: AuthnRequest an die BundID (HTTP-POST-Binding = Formular mit Auto-Submit).
        /// returnUrl muss vom Aufrufer bereits auf lokale Adressen geprüft sein.
        /// </summary>
        /// <exception cref="BundIdUnavailableException">Metadaten der BundID nicht abrufbar.</exception>
        public async Task<IActionResult> StartLoginAsync(HttpResponse response, int requestedLevel, string returnUrl,
            CancellationToken ct)
        {
            var bundId = options.Value;
            Saml2Configuration config;
            try
            {
                config = await configurationProvider.GetConfigurationAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                logger.LogError(ex, "BundID-Metadaten nicht abrufbar");
                throw new BundIdUnavailableException(ex);
            }

            var authnRequest = new Saml2AuthnRequest(config)
            {
                AssertionConsumerServiceUrl = new Uri(bundId.AssertionConsumerServiceUrl),
                ProtocolBinding = ProtocolBindings.HttpPost,
                // Die BundID erwartet das Niveau als STORK-Bezeichner, Vergleich "minimum".
                RequestedAuthnContext = new RequestedAuthnContext
                {
                    Comparison = AuthnContextComparisonTypes.Minimum,
                    AuthnContextClassRef = [TrustLevel.ToStork(requestedLevel)],
                },
                // Angeforderte Attribute und Anzeigename für die BundID-Seite.
                Extensions = AkdbExtension.Create(bundId),
                // Neue Identifizierung erzwingen statt eine bestehende BundID-Sitzung zu nutzen (konfigurierbar).
                ForceAuthn = bundId.ForceAuthn ? true : null,
            };
            var binding = new Saml2PostBinding().Bind(authnRequest);

            // Für die Prüfung der Response merken: Nur eine Antwort auf genau diesen Request wird akzeptiert.
            loginStateStore.Save(response, new BundIdLoginState(authnRequest.IdAsString, requestedLevel, returnUrl));
            return binding.ToActionResult();
        }

        /// <summary>
        /// Prüft die SAML-Response der BundID (HTTP-POST vom Browser an den ACS).
        /// ITfoxtec prüft beim Unbind Signatur (Zertifikat aus den IdP-Metadaten), Issuer, Audience (= eigene EntityID)
        /// und Zeitfenster; hier zusätzlich InResponseTo, Status, Pflichtattribute und Vertrauensniveau.
        /// </summary>
        public async Task<BundIdLoginResult> ProcessResponseAsync(HttpRequest request, HttpResponse response,
            CancellationToken ct)
        {
            // Gemerkten Anmeldezustand holen und sofort verbrauchen: jede Anfrage nur einmal beantwortbar.
            var loginState = loginStateStore.Read(request);
            loginStateStore.Delete(response);
            var requestedLevel = loginState?.RequestedLevel ?? options.Value.DefaultTrustLevel;
            var returnUrl = loginState?.ReturnUrl ?? "/";

            BundIdLoginResult Failed(string title, string message) => new()
            {
                Outcome = BundIdLoginOutcome.Failed,
                RequestedLevel = requestedLevel,
                ReturnUrl = returnUrl,
                ErrorTitle = title,
                ErrorMessage = message,
            };

            try
            {
                var config = await configurationProvider.GetConfigurationAsync(ct);
                var httpRequest = request.ToGenericHttpRequest(validate: true);
                var authnResponse = new Saml2AuthnResponse(config);

                // Zuerst nur Status und InResponseTo lesen: Abbruch oder Fehler enthalten keine Assertion.
                httpRequest.Binding.ReadSamlResponse(httpRequest, authnResponse);

                // Die Antwort muss zu genau der Anfrage gehören, die dieser Browser gestartet hat.
                // ITfoxtec prüft das nicht selbst; ohne diese Prüfung würden auch fremde oder alte Antworten akzeptiert.
                if (loginState == null || authnResponse.InResponseToAsString != loginState.RequestId)
                {
                    logger.LogWarning("BundID-Response ohne passende offene Anfrage (InResponseTo {InResponseTo})",
                        authnResponse.InResponseToAsString);
                    return Failed("Anmeldung abgelaufen",
                        "Diese Anmeldung ist abgelaufen oder wurde bereits verwendet. Bitte melden Sie sich erneut an.");
                }

                if (authnResponse.Status != Saml2StatusCodes.Success)
                {
                    logger.LogInformation("BundID-Anmeldung nicht abgeschlossen: {Status} {Message}",
                        authnResponse.Status, authnResponse.StatusMessage);
                    return new BundIdLoginResult
                    {
                        Outcome = BundIdLoginOutcome.Cancelled,
                        RequestedLevel = requestedLevel,
                        ReturnUrl = returnUrl,
                    };
                }

                httpRequest.Binding.Unbind(httpRequest, authnResponse);

                // SAML-Attribute (OIDs) in verständliche Werte übersetzen.
                var attributes = BundIdAttributes.FromSaml(authnResponse.ClaimsIdentity, options.Value.PostkorbHandleAttribute);

                // Die BundID (bzw. der Simulator) erzwingt das geforderte Niveau nicht zuverlässig – selbst prüfen.
                if (attributes.TrustLevel < loginState.RequestedLevel)
                {
                    return Failed("Vertrauensniveau zu niedrig",
                        $"Für diese Funktion ist das Vertrauensniveau „{TrustLevel.Describe(loginState.RequestedLevel)}“ nötig, " +
                        $"angemeldet wurde mit „{TrustLevel.Describe(attributes.TrustLevel)}“. Bitte melden Sie sich mit einem " +
                        "passenden Identifizierungsmittel an (z. B. ELSTER-Zertifikat oder Online-Ausweis).");
                }

                return new BundIdLoginResult
                {
                    Outcome = BundIdLoginOutcome.Success,
                    RequestedLevel = requestedLevel,
                    ReturnUrl = returnUrl,
                    Attributes = attributes,
                };
            }
            catch (BundIdException ex)
            {
                logger.LogWarning(ex, "BundID-Anmeldung abgelehnt");
                return Failed("Anmeldung nicht möglich", ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Signatur ungültig, falscher Issuer/Audience, abgelaufen, bereits verwendet (Replay) usw.
                logger.LogWarning(ex, "Ungültige SAML-Response der BundID");
                return Failed("Antwort der BundID ungültig",
                    "Die Antwort der BundID konnte nicht bestätigt werden. Bitte melden Sie sich erneut an.");
            }
        }

        /// <summary>
        /// SP-Metadaten: Damit kann die BundID den Dienst als Service Provider einrichten
        /// (EntityID, Adresse des Assertion Consumer Service, Binding, NameID-Format).
        /// </summary>
        public IActionResult CreateMetadata()
        {
            var bundId = options.Value;
            var entityDescriptor = new EntityDescriptor(new Saml2Configuration { Issuer = bundId.SpEntityId })
            {
                ValidUntil = 365,
                SPSsoDescriptor = new SPSsoDescriptor
                {
                    AuthnRequestsSigned = false,
                    WantAssertionsSigned = true,
                    NameIDFormats = [NameIdentifierFormats.Transient],
                    AssertionConsumerServices =
                    [
                        new AssertionConsumerService
                        {
                            Binding = ProtocolBindings.HttpPost,
                            Location = new Uri(bundId.AssertionConsumerServiceUrl),
                        },
                    ],
                },
            };
            return new Saml2Metadata(entityDescriptor).CreateMetadata().ToActionResult();
        }
    }
}
