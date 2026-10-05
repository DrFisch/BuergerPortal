using System.Xml.Linq;
using SamlExtensions = ITfoxtec.Identity.Saml2.Schemas.Extensions;

namespace AuthenticationServer.BundId
{
    /// <summary>
    /// AKDB-Erweiterung des AuthnRequest (Namespace https://www.akdb.de/request/2018/09), wie sie die BundID
    /// erwartet: fordert gezielt Attribute an (Datensparsamkeit) und legt fest, unter welchem Namen der
    /// Online-Dienst auf der BundID-Seite angezeigt wird. Aufbau nach der BundID-Keycloak-Erweiterung der
    /// Bundesagentur für Arbeit (Reihenfolge: RequestedAttributes, DisplayInformation).
    /// </summary>
    public static class AkdbExtension
    {
        private static readonly XNamespace Akdb = "https://www.akdb.de/request/2018/09";
        private static readonly XNamespace ClassicUi = "https://www.akdb.de/request/2018/09/classic-ui/v1";

        public static SamlExtensions Create(BundIdOptions options)
        {
            var authenticationRequest = new XElement(Akdb + "AuthenticationRequest",
                new XAttribute(XNamespace.Xmlns + "akdb", Akdb),
                new XAttribute("Version", "2"),
                new XElement(Akdb + "RequestedAttributes",
                    options.RequestedAttributes.Select(attribute => new XElement(Akdb + "RequestedAttribute",
                        new XAttribute("Name", attribute.Name),
                        new XAttribute("RequiredAttribute", attribute.Required ? "true" : "false")))));

            if (!string.IsNullOrWhiteSpace(options.OrganizationDisplayName))
            {
                authenticationRequest.Add(new XElement(Akdb + "DisplayInformation",
                    new XElement(ClassicUi + "Version",
                        new XAttribute(XNamespace.Xmlns + "classic-ui", ClassicUi),
                        new XElement(ClassicUi + "OrganizationDisplayName", options.OrganizationDisplayName),
                        new XElement(ClassicUi + "OnlineServiceId", options.OnlineServiceId))));
            }

            var extensions = new SamlExtensions();
            extensions.Element.Add(authenticationRequest);
            return extensions;
        }
    }
}
