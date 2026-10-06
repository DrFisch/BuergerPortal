using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BuergerPortal.Api.Extensions
{
    /// <summary>
    /// Autorisierung nach BundID-Vertrauensniveau. Das Niveau steht im Claim "acr" des Access-Tokens
    /// ("STORK-QAA-Level-n", vom Auth-Server aus der BundID-Anmeldung übernommen).
    /// 1 = normal, 3 = substanziell, 4 = hoch.
    /// </summary>
    public static class TrustLevelPolicies
    {
        public const string Substantial = "trustlevel.substantial";
        public const int SubstantialLevel = 3;

        private const string StorkPrefix = "STORK-QAA-Level-";

        // Step-up: Funktionen mit erhöhtem Schutzbedarf verlangen mindestens das BundID-Vertrauensniveau
        // "substanziell" (STORK-QAA-Level-3); sonst 403.
        public static AuthorizationOptions AddTrustLevelPolicies(this AuthorizationOptions options)
        {
            options.AddPolicy(Substantial, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => LevelOf(ctx.User) >= SubstantialLevel));
            return options;
        }

        public static int LevelOf(ClaimsPrincipal user)
        {
            var acr = user.FindFirst("acr")?.Value;
            return acr != null && acr.StartsWith(StorkPrefix, StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(acr[StorkPrefix.Length..], out var level)
                ? level
                : 0;
        }
    }
}
