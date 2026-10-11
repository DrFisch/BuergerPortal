using BuergerPortal.BundId;
using System.Security.Claims;
using System.Text;
using System.Xml;
using Xunit.Abstractions;

namespace BuergerPortal.Tests.BundId
{
    /// <summary>
    /// XML Signature Wrapping (XSW, Somorovsky u. a. 2012): Ein Angreifer behält die echte, signierte Assertion – deren
    /// Signatur bleibt gültig – und setzt daneben eine eigene, unsignierte Assertion mit einer fremden Identität. Prüft
    /// ein Service Provider die Signatur an einer Stelle, liest die Daten aber an einer anderen, meldet er den Angreifer
    /// als fremde Person an. Jede Variante muss abgelehnt werden, und nie darf die fremde Identität herauskommen.
    /// </summary>
    public class SignatureWrappingTests(ITestOutputHelper output)
    {
        private const string SamlNs = "urn:oasis:names:tc:SAML:2.0:assertion";
        private const string SamlpNs = "urn:oasis:names:tc:SAML:2.0:protocol";
        private const string DsNs = "http://www.w3.org/2000/09/xmldsig#";
        private const string Angreifer = "BUNDIDSIM-ANGREIFER";

        private static (XmlDocument Doc, XmlElement Response, XmlElement Signed) Load()
        {
            var doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(SamlTestIdp.CreateResponse())));
            var signed = (XmlElement)doc.GetElementsByTagName("Assertion", SamlNs)[0]!;
            return (doc, doc.DocumentElement!, signed);
        }

        // Kopie der echten Assertion ohne Signatur, mit eigener ID (oder derselben) und der Identität des Angreifers.
        private static XmlElement Evil(XmlElement signed, string? id = null)
        {
            var evil = (XmlElement)signed.CloneNode(deep: true);
            foreach (var signature in evil.GetElementsByTagName("Signature", DsNs).Cast<XmlElement>().ToList())
            {
                signature.ParentNode!.RemoveChild(signature);
            }
            evil.SetAttribute("ID", id ?? "_evil-0001");
            evil.GetElementsByTagName("NameID", SamlNs)[0]!.InnerText = Angreifer;
            foreach (XmlElement value in evil.GetElementsByTagName("AttributeValue", SamlNs))
            {
                if (value.InnerText == "BUNDIDSIM-T01") value.InnerText = Angreifer;
            }
            return evil;
        }

        private static string Save(XmlDocument doc) => Convert.ToBase64String(Encoding.UTF8.GetBytes(doc.OuterXml));

        // Ablauf wie im Auth-Server: Unbind (Signatur, Issuer, Audience, Zeit), dann Empfänger und Anfrage-Bezug.
        private static string? Anmelden(XmlDocument doc)
        {
            var response = SamlTestIdp.Validate(Save(doc));
            BundIdResponseChecks.EnsureAddressedTo(response, SamlTestIdp.Acs, SamlTestIdp.RequestId);
            return response.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? response.ClaimsIdentity.Name;
        }

        public static TheoryData<string> Varianten() =>
        [
            "falsche Assertion vor der echten",
            "falsche Assertion hinter der echten",
            "echte Assertion in Extensions verschoben",
            "echte Assertion in der falschen verschachtelt",
            "falsche Assertion mit derselben ID",
        ];

        [Theory]
        [MemberData(nameof(Varianten))]
        public void Signature_Wrapping_wird_abgelehnt(string variante)
        {
            var (doc, response, signed) = Load();
            switch (variante)
            {
                case "falsche Assertion vor der echten":
                    response.InsertBefore(Evil(signed), signed);
                    break;
                case "falsche Assertion hinter der echten":
                    response.InsertAfter(Evil(signed), signed);
                    break;
                case "echte Assertion in Extensions verschoben":
                {
                    var evil = Evil(signed);
                    response.ReplaceChild(evil, signed);
                    var extensions = doc.CreateElement("samlp", "Extensions", SamlpNs);
                    extensions.AppendChild(signed);
                    var issuer = response.GetElementsByTagName("Issuer", SamlNs)[0]!;
                    response.InsertAfter(extensions, issuer.ParentNode == response ? issuer : response.FirstChild);
                    break;
                }
                case "echte Assertion in der falschen verschachtelt":
                {
                    var evil = Evil(signed);
                    response.ReplaceChild(evil, signed);
                    var advice = doc.CreateElement("saml", "Advice", SamlNs);
                    advice.AppendChild(signed);
                    evil.AppendChild(advice);
                    break;
                }
                case "falsche Assertion mit derselben ID":
                    response.InsertBefore(Evil(signed, signed.GetAttribute("ID")), signed);
                    break;
            }

            string? angemeldet = null;
            var fehler = Record.Exception(() => angemeldet = Anmelden(doc));

            output.WriteLine($"{variante}: {fehler?.GetType().Name}: {fehler?.Message}");
            Assert.NotEqual(Angreifer, angemeldet);
            Assert.NotNull(fehler);
        }

        [Fact]
        public void Unveraenderte_Response_wird_angenommen()
        {
            var (doc, _, _) = Load();
            Assert.Equal("BUNDIDSIM-T01", Anmelden(doc));
        }
    }
}
