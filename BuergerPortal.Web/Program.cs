using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides; // <--- WICHTIG FÜR NGINX
using Microsoft.AspNetCore.DataProtection; // <--- WICHTIG FÜR COOKIES
using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Services ---
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var supportedCultures = new[] { "de", "en" };
builder.Services.Configure<RequestLocalizationOptions>(opts =>
{
    var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    opts.SupportedCultures = cultures;
    opts.SupportedUICultures = cultures;

    opts.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };

    opts.SetDefaultCulture("de");
});

// --- 2. Data Protection (Verhindert Logout bei Server-Neustart) ---
// Schlüssel für Anmelde-Cookie und Antiforgery-Token. Im Container liegen sie in einem Volume
// (DataProtection:KeysPath); ohne Einstellung (lokal) im Benutzerprofil wie bisher.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("BuergerPortal.Web")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// --- 3. AccessTokenHandler für API ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AccessTokenHandler>();
// Erneuert das Access-Token mit dem Refresh-Token (aufgerufen von PortalSession)
builder.Services.AddSingleton<BuergerPortal.Web.Services.AccessTokenRefresher>();
// BundID-Angaben der angemeldeten Person (aus den Claims des ID-Tokens)
builder.Services.AddScoped<BuergerPortal.Web.Services.HttpCurrentUserService>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl not configured");

builder.Services.AddHttpClient("BuergerPortalApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<AccessTokenHandler>();

// --- 4. Authentication ---
var authConfig = builder.Configuration.GetSection("Authentication");

// Ohne Anmeldung ist nur die Einstiegsseite erreichbar: Jede Seite verlangt eine Anmeldung, außer sie ist
// ausdrücklich mit [AllowAnonymous] freigegeben (Einstiegsseite, Anmelde- und Fehlerseiten, Datenschutz).
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build());

// Anmeldeaufforderung: ohne Sitzung zur Einstiegsseite (Leistungsübersicht + BundID-Knopf), mit Sitzung
// (z. B. abgelaufenes Token) direkt zur BundID-Anmeldung über den Auth-Server.
const string LoginOrStartScheme = "BundIdOrStart";

// Sitzungsregeln (Abmeldung nach Inaktivität, Höchstdauer) – siehe Services/PortalSession.cs
BuergerPortal.Web.Services.PortalSession.Configure(builder.Configuration);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = LoginOrStartScheme;
    })
    .AddPolicyScheme(LoginOrStartScheme, "BundID-Anmeldung oder Einstiegsseite", o =>
    {
        o.ForwardDefaultSelector = ctx => ctx.User.Identity?.IsAuthenticated == true
            ? OpenIdConnectDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        // Ablauf nach Inaktivität (30 min); verlängert wird bei jedem Aufruf in PortalSession (OnValidatePrincipal).
        o.ExpireTimeSpan = BuergerPortal.Web.Services.PortalSession.IdleTimeout;
        o.SlidingExpiration = false;
        // Anonym: Einstiegsseite mit Rücksprungziel (/?ReturnUrl=/Termine)
        o.LoginPath = "/";
        o.AccessDeniedPath = "/Auth/LoginRequired";

        // WICHTIG FÜR HTTPS:
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always; 
        o.Cookie.SameSite = SameSiteMode.Lax;

        o.Events = new CookieAuthenticationEvents
        {
            // Bei jedem Aufruf: Gilt die Sitzung noch? Access-Token erneuern oder Sitzung sofort beenden.
            OnValidatePrincipal = BuergerPortal.Web.Services.PortalSession.ValidateAsync,
            OnRedirectToLogin = ctx =>
            {
                // API und Hintergrundabrufe der Seiten (fetch mit X-Requested-With) bekommen 401 statt HTML.
                if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Headers.XRequestedWith == "fetch")
                {
                    ctx.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
                // Abgelaufene Sitzung: Die Einstiegsseite sagt, warum man nicht mehr angemeldet ist.
                var target = BuergerPortal.Web.Services.PortalSession.WasExpired(ctx.HttpContext)
                    ? QueryHelpers.AddQueryString(ctx.RedirectUri, "sitzung", "abgelaufen")
                    : ctx.RedirectUri;
                ctx.Response.Redirect(target);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = 403;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            }
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = authConfig["Authority"];
        options.ClientId = authConfig["ClientId"];
        options.ClientSecret = authConfig["ClientSecret"];
        options.ResponseType = "code";
        options.GetClaimsFromUserInfoEndpoint = true;
        // Claim-Typen so lassen, wie sie im Token stehen ("given_name" statt
        // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"): Mit den BundID-Claims wurde das
        // Sitzungscookie sonst so groß, dass Proxys mit 8-KB-Grenze je Header-Zeile es abweisen könnten.
        options.MapInboundClaims = false;

        // WICHTIG:
        // 1. RequireHttpsMetadata = false lassen, da der Container intern HTTP spricht
        // 2. SecurePolicy auf Always, da Nginx draußen HTTPS macht
        options.RequireHttpsMetadata = false; 
        
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

        options.SaveTokens = true;
        
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("buergerportal_api");
        // BundID-Daten der Person (Name, Geburtsdatum, Adresse, Vertrauensniveau, Postkorb-Handle …)
        options.Scope.Add("bundid");
        // Refresh-Token: Das Access-Token (60 min) wird erneuert, solange die Sitzung gilt.
        options.Scope.Add("offline_access");

        options.MaxAge = TimeSpan.FromHours(24);
        options.BackchannelTimeout = TimeSpan.FromSeconds(3);

        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
        // "acr" (erreichtes Vertrauensniveau) verwirft ASP.NET Core standardmäßig – für Step-up und Anzeige behalten.
        options.ClaimActions.Remove("acr");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = async ctx =>
            {
                // Simple Prüfung ob Auth Server da ist (Timeout 2s)
                if (!await IsAuthorityAlive(ctx.HttpContext.RequestServices, TimeSpan.FromSeconds(2)))
                {
                    ctx.HandleResponse();
                    ctx.Response.Redirect("/auth-down");
                    return;
                }

                // Step-up: Verlangt eine Funktion ein höheres Vertrauensniveau, steht es in den
                // AuthenticationProperties ("acr_values", z. B. "STORK-QAA-Level-3") und geht an den Auth-Server.
                if (ctx.Properties.Items.TryGetValue("acr_values", out var acrValues) && !string.IsNullOrEmpty(acrValues))
                {
                    ctx.ProtocolMessage.AcrValues = acrValues;
                }

                // Jede Anmeldung im Portal ist eine neue BundID-Anmeldung (prompt=login → ForceAuthn): Eine noch
                // bestehende Sitzung am Auth-Server meldet nach Abmeldung oder Zeitablauf nicht still wieder an.
                ctx.ProtocolMessage.Prompt = OpenIdConnectPrompt.Login;
            },
            OnRemoteFailure = ctx =>
            {
                // Technischer Grund ins Log; die Person sieht eine verständliche Seite.
                // access_denied = Anmeldung bei der BundID abgebrochen (vom Auth-Server zurückgemeldet).
                ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                    .LogWarning(ctx.Failure, "OIDC-Anmeldung fehlgeschlagen");
                var grund = ctx.Failure?.Data["error"] as string == "access_denied" ? "abgebrochen" : "fehler";
                ctx.HandleResponse();
                ctx.Response.Redirect("/Auth/Fehler?grund=" + grund);
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                // Sitzungscookie (endet mit dem Browser), Ablauf nach Inaktivität, Anmeldezeit für die Höchstdauer
                BuergerPortal.Web.Services.PortalSession.Start(ctx.Properties!, DateTimeOffset.UtcNow);
                return Task.CompletedTask;
            }
        };
    });

// Hilfsmethode
static async Task<bool> IsAuthorityAlive(IServiceProvider sp, TimeSpan timeout)
{
    var opts = sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                 .Get(OpenIdConnectDefaults.AuthenticationScheme);

    using var cts = new CancellationTokenSource(timeout);
    var wellKnown = opts.MetadataAddress ?? $"{opts.Authority!.TrimEnd('/')}/.well-known/openid-configuration";

    try
    {
        if (opts.Backchannel is not null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, wellKnown);
            using var res = await opts.Backchannel.SendAsync(req, cts.Token);
            return res.IsSuccessStatusCode;
        }

        using var hc = new HttpClient();
        using var res2 = await hc.GetAsync(wellKnown, cts.Token);
        return res2.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}


var app = builder.Build();

// -------------------------------------------------------------------------
// WICHTIG: Forwarded Headers für Nginx (HTTPS Erkennung)
// Muss ganz am Anfang der Pipeline stehen!
// -------------------------------------------------------------------------
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

// WICHTIG: Damit ASP.NET Core dem Docker-Netzwerk vertraut!
// Sonst werden die Header ignoriert und du bleibst auf http hängen.
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedOptions);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // app.UseHsts(); // Macht Nginx/Certbot bereits
}

app.UseStaticFiles();

var locOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// --- Auth Debug Routen ---
app.MapGet("/auth/debug", async (HttpContext ctx) =>
{
    var idToken = await ctx.GetTokenAsync("id_token");
    var accessToken = await ctx.GetTokenAsync("access_token");
    var refreshToken = await ctx.GetTokenAsync("refresh_token");

    var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });

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
        IdTokenPayload = idPayload,
        AccessTokenPayload = accessPayload
    };

    await ctx.Response.WriteAsJsonAsync(result);
}).RequireAuthorization();

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
    if (await IsAuthorityAlive(ctx.RequestServices, TimeSpan.FromSeconds(2)))
    {
        await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
        {
            RedirectUri = "/"
        });
    }
    else
    {
        ctx.Response.Redirect("/?signedout=1&authDown=1");
    }
}).AllowAnonymous();

// Frühere Fehler-Adressen (reiner Text) zeigen jetzt die Fehlerseite /Auth/Fehler.
app.MapGet("/auth-error", () => Results.Redirect("/Auth/Fehler?grund=fehler")).AllowAnonymous();
app.MapGet("/auth-down", () => Results.Redirect("/Auth/Fehler?grund=nicht-erreichbar")).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


// --- AccessTokenHandler Klasse ---
// Hängt das Access-Token der Sitzung an API-Aufrufe. Ablauf und Erneuerung prüft vorher PortalSession
// (OnValidatePrincipal des Cookies) – hier kommt nur noch ein gültiges Token an.
public sealed class AccessTokenHandler(IHttpContextAccessor accessor, ILogger<AccessTokenHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var http = accessor.HttpContext;
        var token = http is null ? null : await http.GetTokenAsync("access_token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            logger.LogDebug("Bearer-Token an {Method} {Uri} angehängt.", req.Method, req.RequestUri);
        }

        return await base.SendAsync(req, ct);
    }
}
