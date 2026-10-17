using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ITfoxtec.Identity.Saml2;
using Microsoft.AspNetCore.WebUtilities;

namespace AuthenticationServer.BundId
{
    /// <summary>Eckdaten eines AuthnRequests: ID, Ziel (SSO der BundID), Aussteller (SP), ACS, Niveau, ForceAuthn.</summary>
    public sealed record AuthnRequestDaten(string Id, string Destination, string Issuer, string AssertionConsumerServiceUrl,
        string Level, bool ForceAuthn);

    /// <summary>
    /// Lernpfad „Schritt für Schritt“ (Wunsch Lukas 2026-10-17): Kommt die OIDC-Anfrage des Portals mit
    /// bpsim_lernpfad=1, zeigt der Auth-Server Schritt 2 (den AuthnRequest vor dem Absenden) und Schritt 4 (die geprüfte
    /// Response) als Erklärseite. Der Merker steckt in der Rücksprungadresse (/connect/authorize?…), die der Auth-Server
    /// ohnehin geschützt im Anmeldezustand hält – es braucht keinen eigenen Zustand.
    /// </summary>
    public static partial class Lernpfad
    {
        public const string Parameter = "bpsim_lernpfad";

        /// <summary>Gehört die Rücksprungadresse zu einer OIDC-Anfrage im Lernpfad?</summary>
        public static bool IsActive(string? returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl)
                || !returnUrl.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            var query = returnUrl.IndexOf('?') is var i and >= 0 ? returnUrl[i..] : string.Empty;
            return QueryHelpers.ParseQuery(query).TryGetValue(Parameter, out var value) && value == "1";
        }

        /// <summary>Wert des Formularfelds SAMLRequest, wie ITfoxtec ihn ins Auto-Submit-Formular schreibt.</summary>
        public static string SamlRequest(Saml2PostBinding binding)
        {
            var match = SamlRequestFeld().Match(binding.PostContent ?? string.Empty);
            return match.Success
                ? WebUtility.HtmlDecode(match.Groups[1].Value)
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(binding.XmlDocument.OuterXml));
        }

        /// <summary>XML eingerückt zum Lesen (die gesendete Nachricht selbst bleibt unverändert).</summary>
        public static string Eingerueckt(XmlDocument document)
        {
            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings
                   { Indent = true, IndentChars = "  ", OmitXmlDeclaration = true, NewLineOnAttributes = false }))
            {
                document.Save(writer);
            }
            return sb.ToString();
        }

        /// <summary>Eckdaten des AuthnRequests für die Erklärseite (aus der gesendeten Nachricht gelesen).</summary>
        public static AuthnRequestDaten Lies(XmlDocument document)
        {
            const string Assertion = "urn:oasis:names:tc:SAML:2.0:assertion";
            var root = document.DocumentElement;
            string Text(string ns, string name) =>
                document.GetElementsByTagName(name, ns).Cast<XmlNode>().FirstOrDefault()?.InnerText.Trim() ?? "–";
            return new AuthnRequestDaten(
                root?.GetAttribute("ID") ?? "–",
                root?.GetAttribute("Destination") ?? string.Empty,
                Text(Assertion, "Issuer"),
                root?.GetAttribute("AssertionConsumerServiceURL") ?? "–",
                Text(Assertion, "AuthnContextClassRef"),
                string.Equals(root?.GetAttribute("ForceAuthn"), "true", StringComparison.OrdinalIgnoreCase));
        }

        [GeneratedRegex("name=\"SAMLRequest\"\\s+value=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
        private static partial Regex SamlRequestFeld();
    }
}
