using System.Security.Claims;
using BuergerPortal.BundId;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BuergerPortal.PostkorbSimulation.Auth;

/// <summary>
/// Inhalt der Postfach-Sitzung: nur Anzeigename, Postkorb-Handle und erreichtes Vertrauensniveau.
/// bPK2, Adresse und weitere BundID-Daten braucht das Postfach nicht und speichert sie deshalb nicht.
/// </summary>
public static class PostfachSession
{
    public const string NameClaim = "name";

    public static ClaimsPrincipal Create(string displayName, Guid postkorbHandle, int trustLevel)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme, NameClaim, roleType: null);
        identity.AddClaim(new Claim(NameClaim, displayName));
        identity.AddClaim(new Claim(BundIdClaimTypes.PostkorbHandle, postkorbHandle.ToString()));
        identity.AddClaim(new Claim(BundIdClaimTypes.TrustLevel, TrustLevel.ToStork(trustLevel)));
        return new ClaimsPrincipal(identity);
    }

    public static Guid? GetPostkorbHandle(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(BundIdClaimTypes.PostkorbHandle), out var handle) ? handle : null;

    /// <summary>Vertrauensniveau der Sitzung (1, 3, 4); 0 ohne Anmeldung.</summary>
    public static int GetTrustLevel(this ClaimsPrincipal user) =>
        TrustLevel.Parse(user.FindFirstValue(BundIdClaimTypes.TrustLevel)) ?? 0;
}
