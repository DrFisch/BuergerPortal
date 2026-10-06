using BuergerPortal.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace BuergerPortal.Tests.Api
{
    /// <summary>Policy der API für Funktionen ab BundID-Niveau "substanziell" (Step-up).</summary>
    public class TrustLevelPoliciesTests
    {
        private static ClaimsPrincipal User(string? acr)
        {
            var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
            if (acr != null) claims.Add(new Claim("acr", acr));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"));
        }

        private static async Task<bool> IsAllowed(ClaimsPrincipal user)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(o => o.AddTrustLevelPolicies());
            var authorization = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
            return (await authorization.AuthorizeAsync(user, null, TrustLevelPolicies.Substantial)).Succeeded;
        }

        [Theory]
        [InlineData("STORK-QAA-Level-1", 1)]
        [InlineData("STORK-QAA-Level-3", 3)]
        [InlineData("STORK-QAA-Level-4", 4)]
        [InlineData("irgendwas", 0)]
        [InlineData(null, 0)]
        public void LevelOf_liest_das_Niveau_aus_acr(string? acr, int expected)
        {
            Assert.Equal(expected, TrustLevelPolicies.LevelOf(User(acr)));
        }

        [Theory]
        [InlineData("STORK-QAA-Level-1", false)]
        [InlineData(null, false)]
        [InlineData("STORK-QAA-Level-3", true)]
        [InlineData("STORK-QAA-Level-4", true)]
        public async Task Policy_verlangt_mindestens_substanziell(string? acr, bool allowed)
        {
            Assert.Equal(allowed, await IsAllowed(User(acr)));
        }

        [Fact]
        public async Task Policy_lehnt_Anonyme_ab()
        {
            Assert.False(await IsAllowed(new ClaimsPrincipal(new ClaimsIdentity())));
        }
    }
}
