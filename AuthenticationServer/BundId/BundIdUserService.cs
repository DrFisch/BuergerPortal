using AuthenticationServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationServer.BundId
{
    /// <summary>
    /// Verbindet eine BundID-Anmeldung mit einem Benutzerkonto des Portals: Wiedererkennung über die bPK2,
    /// beim ersten Login wird das Konto automatisch angelegt (Just-in-Time-Provisioning).
    /// </summary>
    public sealed class BundIdUserService(UserManager<ApplicationUser> userManager, ILogger<BundIdUserService> logger)
    {
        public async Task<(ApplicationUser User, bool Created)> FindOrCreateAsync(BundIdAttributes attributes,
            CancellationToken ct = default)
        {
            var user = await userManager.Users.SingleOrDefaultAsync(u => u.Bpk2 == attributes.Bpk2, ct);
            if (user != null)
            {
                return (user, false);
            }

            user = new ApplicationUser
            {
                // Technischer Benutzername: Die bPK2 selbst eignet sich nicht (Sonderzeichen, personenbezogen).
                UserName = $"bundid-{Guid.NewGuid():N}",
                Bpk2 = attributes.Bpk2,
                CreatedViaBundIdUtc = DateTime.UtcNow,
            };
            // Kein Passwort: Dieses Konto kann sich nur über die BundID anmelden.
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                logger.LogError("Konto für BundID-Login nicht angelegt: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                throw new BundIdException("Das Benutzerkonto konnte nicht angelegt werden. Bitte versuchen Sie es erneut.");
            }

            logger.LogInformation("Neues Konto {UserId} beim ersten BundID-Login angelegt", user.Id);
            return (user, true);
        }
    }
}
