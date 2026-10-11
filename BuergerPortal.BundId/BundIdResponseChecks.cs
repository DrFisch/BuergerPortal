using ITfoxtec.Identity.Saml2;

namespace BuergerPortal.BundId
{
    /// <summary>Die Antwort ist nicht für diesen Service Provider bzw. diese Anfrage bestimmt (Grund nur fürs Protokoll).</summary>
    public sealed class BundIdInvalidResponseException(string reason) : Exception(reason);

    /// <summary>
    /// Prüfungen, die das Web Browser SSO Profile vom Service Provider verlangt (SAML Profiles §4.1.4.3), die ITfoxtec
    /// beim Unbind aber nicht selbst übernimmt (belegt durch <c>ResponseValidationTests</c>): Die Antwort muss an den
    /// eigenen Assertion Consumer Service gerichtet sein, und die signierte Bestätigung muss zur eigenen Anfrage gehören.
    /// </summary>
    public static class BundIdResponseChecks
    {
        private const string BearerMethod = "urn:oasis:names:tc:SAML:2.0:cm:bearer";

        /// <exception cref="BundIdInvalidResponseException">Empfänger oder Anfrage-Bezug passen nicht.</exception>
        public static void EnsureAddressedTo(Saml2AuthnResponse response, Uri assertionConsumerService, string requestId)
        {
            // Destination steht in der (unsignierten) Response; fehlt sie, entscheidet der signierte Recipient unten.
            if (response.Destination != null && !SameAddress(response.Destination, assertionConsumerService))
            {
                throw new BundIdInvalidResponseException(
                    $"Destination {response.Destination} statt {assertionConsumerService}");
            }

            // Bearer heißt: Wer die Assertion vorlegt, gilt als die Person. Deshalb muss die Bestätigung im signierten
            // Teil genau diesen ACS als Empfänger nennen und auf genau diese Anfrage antworten – sonst könnte eine für
            // einen anderen Dienst oder eine andere Anmeldung ausgestellte Assertion hier eingelöst werden.
            var confirmations = response.Saml2SecurityToken?.Assertion.Subject?.SubjectConfirmations ?? [];
            var matches = confirmations.Any(c =>
                c.Method?.OriginalString == BearerMethod
                && c.SubjectConfirmationData?.Recipient is { } recipient
                && SameAddress(recipient, assertionConsumerService)
                && c.SubjectConfirmationData.InResponseTo?.Value == requestId);
            if (!matches)
            {
                throw new BundIdInvalidResponseException(
                    $"Keine Bearer-Bestätigung für ACS {assertionConsumerService} und Anfrage {requestId}");
            }
        }

        private static bool SameAddress(Uri a, Uri b) =>
            string.Equals(a.AbsoluteUri.TrimEnd('/'), b.AbsoluteUri.TrimEnd('/'), StringComparison.Ordinal);
    }
}
