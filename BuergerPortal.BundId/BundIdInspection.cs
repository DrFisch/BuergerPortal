using ITfoxtec.Identity.Saml2;
using Microsoft.IdentityModel.Tokens.Saml2;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace BuergerPortal.BundId
{
    /// <summary>Eine bestandene Prüfung der SAML-Response: was erwartet wurde, was ankam und wer geprüft hat.</summary>
    public sealed record BundIdInspectionCheck(string Name, string Purpose, string Expected, string Actual, string CheckedBy);

    /// <summary>Ein SAML-Attribut der Assertion und was der Auth-Server daraus macht (Claim, Verwendung).</summary>
    public sealed record BundIdInspectionAttribute(string Name, string? FriendlyName, IReadOnlyList<string> Values,
        string? Claim, string Usage);

    /// <summary>
    /// Prüfprotokoll einer erfolgreichen BundID-Anmeldung für den SAML-Inspektor: die empfangene Response (dekodiert,
    /// eingerückt), je Prüfung Soll und Ist, und die Abbildung Attribut → Claim. Enthält keine Tokens; Signaturwert,
    /// Zertifikat und bPK2 sind gekürzt, damit die Anzeige weder als Assertion wiederverwendbar ist noch die volle
    /// Kennung zeigt (wie „Meine Daten“).
    /// </summary>
    public sealed record BundIdInspection
    {
        private const string SamlProtocolNs = "urn:oasis:names:tc:SAML:2.0:protocol";
        private const string SamlAssertionNs = "urn:oasis:names:tc:SAML:2.0:assertion";
        private const string XmlDsigNs = "http://www.w3.org/2000/09/xmldsig#";

        public required DateTime ReceivedUtc { get; init; }
        public required string RequestId { get; init; }
        public required string AssertionId { get; init; }
        public required string Issuer { get; init; }
        public required int RequestedLevel { get; init; }
        public required int ReachedLevel { get; init; }
        public required IReadOnlyList<BundIdInspectionCheck> Checks { get; init; }
        public required IReadOnlyList<BundIdInspectionAttribute> Attributes { get; init; }
        public required string ResponseXml { get; init; }

        /// <summary>
        /// Baut das Protokoll nach allen bestandenen Prüfungen (Aufruf am Ende von
        /// <see cref="BundIdSamlService.ProcessResponseAsync"/>). Alle Werte stammen aus der geprüften Response.
        /// </summary>
        public static BundIdInspection Create(Saml2AuthnResponse response, Saml2Configuration config,
            BundIdLoginState loginState, BundIdAttributes attributes, BundIdOptions options, DateTime receivedUtc)
        {
            var assertion = response.Saml2SecurityToken.Assertion;
            var document = response.XmlDocument;
            var ns = NamespaceManager(document);
            var statusCode = (document.SelectSingleNode("//samlp:Status/samlp:StatusCode", ns) as XmlElement)?.GetAttribute("Value");
            var assertionElement = (XmlElement?)document.SelectSingleNode("//saml:Assertion", ns);
            var signature = (XmlElement?)assertionElement?.SelectSingleNode("ds:Signature", ns)
                ?? (XmlElement?)document.DocumentElement?.SelectSingleNode("ds:Signature", ns);

            var confirmation = assertion.Subject?.SubjectConfirmations
                .Select(c => c.SubjectConfirmationData).FirstOrDefault(d => d?.Recipient != null);
            var conditions = assertion.Conditions;
            var classRefs = assertion.Statements.OfType<Saml2AuthenticationStatement>()
                .Select(s => s.AuthenticationContext?.ClassReference?.OriginalString)
                .OfType<string>().ToList();

            var checks = new List<BundIdInspectionCheck>
            {
                new("Status", "Die BundID meldet eine abgeschlossene Anmeldung (kein Abbruch, kein Fehler).",
                    "urn:oasis:names:tc:SAML:2.0:status:Success", statusCode ?? response.Status.ToString(),
                    "eigener Code (BundIdSamlService)"),
                SignatureCheck(signature, assertion.Id.Value, config),
                new("Aussteller (Issuer)", "Die Antwort stammt von der BundID, deren Metadaten geladen wurden.",
                    config.AllowedIssuer, $"Response: {response.Issuer} · Assertion: {assertion.Issuer.Value}",
                    "ITfoxtec (Unbind)"),
                new("Zielgruppe (Audience)", "Die Assertion ist für genau diesen Service Provider ausgestellt.",
                    options.SpEntityId,
                    string.Join(", ", conditions?.AudienceRestrictions.SelectMany(a => a.Audiences)
                        .Select(a => a.ToString()) ?? []),
                    "ITfoxtec (Unbind)"),
                new("Empfänger (Destination, Recipient)",
                    "Die Antwort ist an den eigenen Assertion Consumer Service gerichtet (Bearer: wer sie vorlegt, gilt als die Person).",
                    options.AssertionConsumerServiceUrl,
                    $"Destination: {response.Destination?.OriginalString ?? "–"} · Recipient: {confirmation?.Recipient?.OriginalString ?? "–"}",
                    "eigener Code (BundIdResponseChecks)"),
                new("Zeitfenster", "Die Assertion ist zum Zeitpunkt des Empfangs gültig – alte Antworten verfallen.",
                    $"Empfang zwischen NotBefore und NotOnOrAfter (empfangen {Utc(receivedUtc)})",
                    $"NotBefore {Utc(conditions?.NotBefore)} · NotOnOrAfter {Utc(conditions?.NotOnOrAfter)}",
                    "ITfoxtec (Unbind)"),
                new("Anfrage-Bezug (InResponseTo)",
                    "Die Antwort gehört zu der Anfrage, die dieser Browser gestartet hat (Request-ID im Cookie bundid_login).",
                    loginState.RequestId,
                    $"Response: {response.InResponseToAsString} · signierte Bestätigung: {confirmation?.InResponseTo?.Value ?? "–"}",
                    "eigener Code (BundIdSamlService, BundIdResponseChecks)"),
                new("Einmaligkeit (Replay)", "Dieselbe Assertion kann kein zweites Mal eingelöst werden.",
                    "Assertion-ID noch nie verarbeitet",
                    $"{assertion.Id.Value} jetzt bis {Utc(conditions?.NotOnOrAfter)} im Replay-Cache vermerkt",
                    "ITfoxtec (DetectReplayedTokens) mit eigenem Cache"),
                new("Vertrauensniveau", "Das Identifizierungsmittel war mindestens so stark wie gefordert.",
                    $"mindestens {TrustLevel.Describe(loginState.RequestedLevel)} ({TrustLevel.ToStork(loginState.RequestedLevel)})",
                    $"AuthnContextClassRef: {string.Join(", ", classRefs)} · Attribut EID-CITIZEN-QAA-LEVEL: " +
                    $"{attributes.QaaLevelAttribute ?? "–"} → {TrustLevel.Describe(attributes.TrustLevel)}",
                    "eigener Code (BundIdSamlService); API zusätzlich per Policy"),
                new("Rücksprungziel", "Nach der Anmeldung geht es nur zu einer Adresse dieses Auth-Servers (kein Open Redirect).",
                    "lokale Adresse", loginState.ReturnUrl.Split('?')[0], "eigener Code (BundIdController)"),
            };

            return new BundIdInspection
            {
                ReceivedUtc = receivedUtc,
                RequestId = loginState.RequestId,
                AssertionId = assertion.Id.Value,
                Issuer = assertion.Issuer.Value,
                RequestedLevel = loginState.RequestedLevel,
                ReachedLevel = attributes.TrustLevel,
                Checks = checks,
                Attributes = ReadAttributes(assertionElement, ns, attributes, options),
                ResponseXml = FormatXml(document, attributes.Bpk2),
            };
        }

        private static BundIdInspectionCheck SignatureCheck(XmlElement? signature, string assertionId, Saml2Configuration config)
        {
            string? Algorithm(string path) =>
                (signature?.SelectSingleNode(path, NamespaceManager(signature.OwnerDocument)) as XmlElement)?.GetAttribute("Algorithm");
            var reference = (signature?.SelectSingleNode("ds:SignedInfo/ds:Reference",
                NamespaceManager(signature.OwnerDocument)) as XmlElement)?.GetAttribute("URI");
            var metadataCertificates = config.SignatureValidationCertificates
                .Select(c => $"{c.GetNameInfo(X509NameType.SimpleName, false)} (SHA-256 {Fingerprint(c)})");

            var embedded = (signature?.SelectSingleNode("ds:KeyInfo/ds:X509Data/ds:X509Certificate",
                NamespaceManager(signature.OwnerDocument)) as XmlElement)?.InnerText;
            var embeddedText = "kein Zertifikat in der Signatur";
            if (!string.IsNullOrWhiteSpace(embedded))
            {
                try
                {
                    var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(Regex.Replace(embedded, @"\s", "")));
                    var same = config.SignatureValidationCertificates.Any(c => c.RawData.AsSpan().SequenceEqual(certificate.RawData));
                    embeddedText = $"Zertifikat in der Signatur: SHA-256 {Fingerprint(certificate)} – " +
                        (same ? "gleich dem aus den Metadaten" : "nicht das aus den Metadaten (geprüft wurde gegen die Metadaten)");
                }
                catch (Exception ex) when (ex is FormatException or CryptographicException)
                {
                    embeddedText = "Zertifikat in der Signatur nicht lesbar (geprüft wurde gegen die Metadaten)";
                }
            }

            return new BundIdInspectionCheck("Signatur",
                "Die Assertion ist unverändert und mit dem Schlüssel der BundID signiert; vertraut wird nur dem Zertifikat aus deren Metadaten.",
                $"gültige XML-Signatur mit dem Schlüssel aus den IdP-Metadaten: {string.Join("; ", metadataCertificates)}",
                $"{Short(Algorithm("ds:SignedInfo/ds:SignatureMethod"))} über {reference} " +
                $"({(reference == "#" + assertionId ? "= die Assertion" : "nicht die Assertion")}), Kanonisierung " +
                $"{Short(Algorithm("ds:SignedInfo/ds:CanonicalizationMethod"))}, Digest " +
                $"{Short(Algorithm("ds:SignedInfo/ds:Reference/ds:DigestMethod"))}; {embeddedText}",
                "ITfoxtec (Unbind)");
        }

        // Attribut → Claim: wie BundIdAttributes.FromSaml/ToClaims und AuthorizationController (Claim-Ziele) es machen.
        private static IReadOnlyList<BundIdInspectionAttribute> ReadAttributes(XmlElement? assertionElement,
            XmlNamespaceManager ns, BundIdAttributes attributes, BundIdOptions options)
        {
            const string IdToken = "ID-Token";
            const string BothTokens = "ID- und Access-Token";
            const string Name = "ID-Token; mit Vor- bzw. Nachnamen als „name“ auch im Access-Token";
            const string Address = "ID-Token (Claim „address“ als JSON)";
            var mapping = new Dictionary<string, (string Claim, string Usage)>
            {
                [BundIdOids.Bpk2] = (BundIdClaimTypes.Bpk2, "Konto-Zuordnung (gespeichert), " + IdToken),
                [BundIdOids.GivenName] = (BundIdClaimTypes.GivenName, Name),
                [BundIdOids.Surname] = (BundIdClaimTypes.FamilyName, Name),
                [BundIdOids.Mail] = (BundIdClaimTypes.Email, BothTokens),
                [BundIdOids.Birthdate] = (BundIdClaimTypes.Birthdate, IdToken),
                [BundIdOids.PlaceOfBirth] = (BundIdClaimTypes.PlaceOfBirth, IdToken),
                [BundIdOids.BirthName] = (BundIdClaimTypes.BirthName, IdToken),
                [BundIdOids.PostalAddress] = ("address.street_address", Address),
                [BundIdOids.PostalCode] = ("address.postal_code", Address),
                [BundIdOids.LocalityName] = ("address.locality", Address),
                [BundIdOids.Country] = ("address.country", Address),
                [BundIdOids.EidCitizenQaaLevel] = (BundIdClaimTypes.TrustLevel,
                    "mit dem AuthnContextClassRef verglichen (der niedrigere Wert gilt); " + BothTokens + ", gespeichert"),
                [BundIdOids.AssertionProvedBy] = (BundIdClaimTypes.IdentificationMethod, IdToken),
                [Normalize(options.PostkorbHandleAttribute)] = (BundIdClaimTypes.PostkorbHandle, BothTokens + ", gespeichert"),
            };
            mapping = mapping.ToDictionary(e => Normalize(e.Key), e => e.Value);

            var result = new List<BundIdInspectionAttribute>();
            if (assertionElement == null) return result;
            foreach (XmlElement attribute in assertionElement.SelectNodes("saml:AttributeStatement/saml:Attribute", ns)!)
            {
                var name = attribute.GetAttribute("Name");
                var values = attribute.SelectNodes("saml:AttributeValue", ns)!.Cast<XmlElement>()
                    .Select(v => Normalize(name) == Normalize(BundIdOids.Bpk2) ? MaskBpk2(v.InnerText) : v.InnerText).ToList();
                var known = mapping.TryGetValue(Normalize(name), out var target);
                result.Add(new BundIdInspectionAttribute(name,
                    attribute.HasAttribute("FriendlyName") ? attribute.GetAttribute("FriendlyName") : null, values,
                    known ? target.Claim : null, known ? target.Usage : "nicht verwendet"));
            }
            return result;
        }

        // Eingerückt, ohne DTD; Signaturwert und Zertifikat gekürzt, bPK2 wie in „Meine Daten“ gekürzt.
        private static string FormatXml(XmlDocument document, string bpk2)
        {
            var copy = new XmlDocument { PreserveWhitespace = false };
            using (var reader = XmlReader.Create(new StringReader(document.OuterXml),
                       new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                copy.Load(reader);
            }
            var ns = NamespaceManager(copy);
            foreach (XmlElement element in copy.SelectNodes("//ds:SignatureValue | //ds:X509Certificate", ns)!)
            {
                var value = Regex.Replace(element.InnerText, @"\s", "");
                element.InnerText = value.Length <= 40 ? value : $"{value[..24]}… (gekürzt, {value.Length} Zeichen)";
            }
            foreach (XmlNode text in copy.SelectNodes("//text()[normalize-space(.) != '']")!)
            {
                if (text.Value?.Trim() == bpk2) text.Value = MaskBpk2(bpk2);
            }

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, new XmlWriterSettings
                   { Indent = true, IndentChars = "  ", OmitXmlDeclaration = true }))
            {
                copy.Save(writer);
            }
            return builder.ToString();
        }

        private static XmlNamespaceManager NamespaceManager(XmlDocument document)
        {
            var ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("samlp", SamlProtocolNs);
            ns.AddNamespace("saml", SamlAssertionNs);
            ns.AddNamespace("ds", XmlDsigNs);
            return ns;
        }

        // Wie BundIdUser.MaskedBpk2 im Portal: Anfang und Ende, nie die volle Kennung.
        public static string MaskBpk2(string bpk2) =>
            bpk2.Length <= 8 ? new string('•', bpk2.Length) : $"{bpk2[..4]}…{bpk2[^4..]}";

        private static string Fingerprint(X509Certificate2 certificate) =>
            string.Join(' ', certificate.GetCertHashString(HashAlgorithmName.SHA256).Chunk(4).Select(c => new string(c)));

        // Algorithmus-URI ohne Namensraum-Präfix, z. B. "…xmldsig-more#rsa-sha256" → "rsa-sha256".
        private static string Short(string? uri) => string.IsNullOrEmpty(uri) ? "–" : uri[(uri.LastIndexOf('#') + 1)..];

        private static string Utc(DateTime? value) =>
            value is { } v ? v.ToUniversalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " UTC" : "–";

        private static string Normalize(string oid) => string.Concat(oid.Where(c => !char.IsWhiteSpace(c)));
    }
}
