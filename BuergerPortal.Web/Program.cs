using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ---- (1) AccessTokenHandler für Bearer-Token an die API
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AccessTokenHandler>();

builder.Services.AddHttpClient("BuergerPortalApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7003/"); 
})
.AddHttpMessageHandler<AccessTokenHandler>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;          // "Cookies"
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;       // "oidc"
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        // UX: bei 401 automatisch zum Login
        o.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 401; return Task.CompletedTask; }
                ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask;
            }
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = "https://localhost:7001";
        options.ClientId = "mvc_web";
        options.ClientSecret = "dev_secret_very_long";
        options.ResponseType = "code";
        options.ResponseMode = "form_post";
        options.SaveTokens = true;

        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("buergerportal_api");   // <-- der API-Scope muss am AuthServer für Client mvc_web erlaubt sein

        // --- Claim-Mapping
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        // wichtig: sub -> NameIdentifier, falls du das z. B. in der API nutzt
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };

        // Dev-Quality-of-life:
        options.RequireHttpsMetadata = true; // bei https-Dev bleibt das true
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    var auth = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (auth.Succeeded)
    {
        var expiresAt = auth.Properties?.GetTokenValue("expires_at"); // kommt von SaveTokens = true
        if (!string.IsNullOrEmpty(expiresAt) &&
            DateTimeOffset.TryParse(expiresAt, out var expUtc))
        {
            // Wenn abgelaufen -> Logout + sofort neu anmelden (Challenge)
            if (expUtc <= DateTimeOffset.UtcNow)
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);

                // Zurück auf dieselbe Seite nach Login
                await ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
                {
                    RedirectUri = ctx.Request.Path + ctx.Request.QueryString
                });
                return; // Request hier beenden
            }
        }
    }
    await next();
});
app.UseAuthorization();

app.MapGet("/auth/debug", async (HttpContext ctx) =>
{
    var idToken = await ctx.GetTokenAsync("id_token");
    var accessToken = await ctx.GetTokenAsync("access_token");
    var refreshToken = await ctx.GetTokenAsync("refresh_token");

    // Alle Claims aus dem Cookie-Principal (basieren meist auf ID Token + UserInfo)
    var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });

    // Hilfsfunktion: JWT-Payload ohne Validierung decodieren
    static string? DecodeJwtPayload(string? jwt)
    {
        if (string.IsNullOrEmpty(jwt)) return null;
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;
        string Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            var bytes = Convert.FromBase64String(s);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        return Base64UrlDecode(parts[1]);
    }

    var idPayload = DecodeJwtPayload(idToken);
    var accessPayload = DecodeJwtPayload(accessToken);

    var result = new
    {
        Authenticated = ctx.User.Identity?.IsAuthenticated,
        Name = ctx.User.Identity?.Name,
        Claims = claims,
        Tokens = new
        {
            HasIdToken = idToken != null,
            HasAccessToken = accessToken != null,
            HasRefreshToken = refreshToken != null
        },
        IdTokenPayload = idPayload,         // JSON-String
        AccessTokenPayload = accessPayload  // JSON-String (hier stehen aud/scope/sub)
    };

    await ctx.Response.WriteAsJsonAsync(result);
}).RequireAuthorization(); // nur für angemeldete Nutzer

// ---- (3) schlanke Login/Logout-Routen
app.MapGet("/login", async (HttpContext ctx) =>
{
    await ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


/// <summary>
/// Hängt das Access Token aus dem Auth-Cookie als Bearer an alle API-Requests.
/// </summary>
public sealed class AccessTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<AccessTokenHandler> _logger;
    public AccessTokenHandler(IHttpContextAccessor accessor, ILogger<AccessTokenHandler> logger)
    { _accessor = accessor; _logger = logger; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var http = _accessor.HttpContext;
        if (http is null)
        {
            _logger.LogWarning("No HttpContext in AccessTokenHandler.");
            return await base.SendAsync(req, ct); // => kein Token → 401
        }

        var token = await http.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("No access_token found. User authenticated? {Auth}", http.User?.Identity?.IsAuthenticated);
            // Optional hart: throw new InvalidOperationException("Kein access_token → bitte neu einloggen.");
            return await base.SendAsync(req, ct);
        }

        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        _logger.LogInformation("Attached Bearer token ({Len} chars) to {Method} {Uri}", token.Length, req.Method, req.RequestUri);

        return await base.SendAsync(req, ct);
    }
}
