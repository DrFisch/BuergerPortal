using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace BuergerPortal.PostkorbSimulation.Api;

/// <summary>
/// Absender-Prüfung der REST-Schnittstelle: gemeinsamer API-Schlüssel im Header "X-Api-Key".
/// Vereinfachung gegenüber dem echten ZBP, das ein BPKI-Zertifikat und eine Signatur über den Inhalt verlangt.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices.GetRequiredService<IOptions<PostkorbApiOptions>>().Value.ApiKey;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!Matches(provided, expected))
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "API-Schlüssel fehlt oder ist ungültig.",
            });
        }
    }

    // Vergleich über die Hashwerte in konstanter Zeit: verrät weder Inhalt noch Länge des Schlüssels.
    public static bool Matches(string provided, string expected) =>
        provided.Length > 0 && expected.Length > 0 &&
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
