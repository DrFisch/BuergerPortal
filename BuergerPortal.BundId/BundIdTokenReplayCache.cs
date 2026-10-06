using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;

namespace BuergerPortal.BundId
{
    /// <summary>
    /// Merkt sich bereits verarbeitete Assertions bis zu ihrem Ablauf. Wird dieselbe Assertion ein zweites Mal
    /// vorgelegt, lehnt die SAML-Bibliothek sie ab (Replay-Schutz). Im Speicher – genügt für eine Instanz des
    /// Auth-Servers; bei mehreren Instanzen wäre ein gemeinsamer Cache nötig.
    /// </summary>
    public sealed class BundIdTokenReplayCache : ITokenReplayCache
    {
        private readonly ConcurrentDictionary<string, DateTime> seenTokens = new();

        public bool TryAdd(string securityToken, DateTime expiresOn)
        {
            RemoveExpired();
            return seenTokens.TryAdd(securityToken, expiresOn);
        }

        public bool TryFind(string securityToken) => seenTokens.ContainsKey(securityToken);

        private void RemoveExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in seenTokens.Where(e => e.Value < now))
            {
                seenTokens.TryRemove(entry.Key, out _);
            }
        }
    }
}
