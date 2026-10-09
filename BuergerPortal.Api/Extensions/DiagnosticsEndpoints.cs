using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BuergerPortal.Api.Extensions
{
    /// <summary>
    /// Markiert Controller, die nur zur Diagnose in der Entwicklung dienen (z. B. <c>api/debug</c>, <c>api/email/test</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DiagnosticsEndpointAttribute : Attribute
    {
    }

    /// <summary>
    /// Diagnose-Controller gibt es nur in der Umgebung Development oder ausdrücklich mit <c>Diagnostics:Enabled=true</c>.
    /// Sonst werden sie gar nicht erst registriert – ihre Adressen antworten mit 404. Die API ist im Betrieb zwar nur
    /// intern erreichbar, ein Echo des Authorization-Headers oder ein anonymer Mailversand gehören trotzdem nicht dorthin.
    /// </summary>
    public sealed class DiagnosticsEndpointConvention(bool enabled) : IApplicationModelConvention
    {
        public const string ConfigKey = "Diagnostics:Enabled";

        public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
            configuration.GetValue<bool?>(ConfigKey) ?? environment.IsDevelopment();

        public void Apply(ApplicationModel application)
        {
            if (enabled) return;
            foreach (var controller in application.Controllers
                         .Where(c => c.Attributes.OfType<DiagnosticsEndpointAttribute>().Any()).ToList())
            {
                application.Controllers.Remove(controller);
            }
        }
    }
}
