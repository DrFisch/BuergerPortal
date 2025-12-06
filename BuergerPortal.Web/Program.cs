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
// Der Ordner "keys" muss via Docker Volume persistiert werden!
// Falls du kein Volume hast, lösch diesen Block, aber dann fliegst du beim Neustart raus.

// --- 3. AccessTokenHandler für API ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AccessTokenHandler>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl not configured");

builder.Services.AddHttpClient("BuergerPortalApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<AccessTokenHandler>();

// --- 4. Authentication ---
var authConfig = builder.Configuration.GetSection("Authentication");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        o.ExpireTimeSpan = TimeSpan.FromHours(24);
        o.SlidingExpiration = false;
        o.LoginPath = "/Auth/LoginRequired";
        o.AccessDeniedPath = "/Auth/LoginRequired";

        // WICHTIG FÜR HTTPS:
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always; 
        o.Cookie.SameSite = SameSiteMode.Lax;

        o.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
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

        options.MaxAge = TimeSpan.FromHours(24);
        options.BackchannelTimeout = TimeSpan.FromSeconds(3);

        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");

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
                }
            },
            OnRemoteFailure = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.Redirect("/auth-error?reason=" + Uri.EscapeDataString(ctx.Failure?.Message ?? ""));
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                ctx.Properties.IsPersistent = false;
                ctx.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24);
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
});

app.MapGet("/auth-error", async ctx =>
{
    var reason = ctx.Request.Query["reason"].ToString();
    var text = string.IsNullOrWhiteSpace(reason)
        ? "Authentifizierungsfehler."
        : $"Authentifizierungsfehler: {WebUtility.UrlDecode(reason)}";
    ctx.Response.ContentType = "text/plain; charset=utf-8";
    await ctx.Response.WriteAsync(text);
}).AllowAnonymous();

app.MapGet("/auth-down", async ctx =>
{
    ctx.Response.ContentType = "text/plain; charset=utf-8";
    await ctx.Response.WriteAsync(
        "Der Anmeldedienst ist derzeit nicht erreichbar. Bitte später erneut versuchen."
    );
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


// --- AccessTokenHandler Klasse ---
public sealed class AccessTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<AccessTokenHandler> _logger;
    private readonly IOptionsMonitor<OpenIdConnectOptions> _oidcOptions;
    private readonly IHttpClientFactory _httpClientFactory;

    public AccessTokenHandler(IHttpContextAccessor accessor, ILogger<AccessTokenHandler> logger,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions, IHttpClientFactory httpClientFactory)
    {
        _accessor = accessor; _logger = logger; _oidcOptions = oidcOptions; _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var http = _accessor.HttpContext;
        if (http is null)
        {
            _logger.LogWarning("No HttpContext in AccessTokenHandler.");
            return await base.SendAsync(req, ct);
        }

        var token = await http.GetTokenAsync("access_token");
        var expiresAtStr = await http.GetTokenAsync("expires_at");
        DateTimeOffset? expiresAt = null;
        if (!string.IsNullOrWhiteSpace(expiresAtStr) && DateTimeOffset.TryParse(expiresAtStr, out var dt))
        {
            expiresAt = dt;
        }

        // Token Expired -> Logout
        if (expiresAt.HasValue && DateTimeOffset.UtcNow >= expiresAt.Value)
        {
            _logger.LogInformation("Access token expired; signing out user.");
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return await base.SendAsync(req, ct);
        }

        // Refresh logic
        if (!string.IsNullOrWhiteSpace(token) && (!expiresAt.HasValue || expiresAt.Value - DateTimeOffset.UtcNow <= TimeSpan.FromMinutes(5)))
        {
            var refreshToken = await http.GetTokenAsync("refresh_token");
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    var opts = _oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
                    var tokenEndpoint = opts.Configuration?.TokenEndpoint ?? $"{opts.Authority!.TrimEnd('/')}/connect/token";

                    var client = _httpClientFactory.CreateClient();
                    var pairs = new List<KeyValuePair<string, string>>
                    {
                        new("grant_type", "refresh_token"),
                        new("refresh_token", refreshToken),
                        new("client_id", opts.ClientId ?? string.Empty),
                        new("client_secret", opts.ClientSecret ?? string.Empty)
                    };

                    var res = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(pairs), ct);
                    if (res.IsSuccessStatusCode)
                    {
                        using var stream = await res.Content.ReadAsStreamAsync(ct);
                        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                        var root = doc.RootElement;
                        var newAccess = root.GetProperty("access_token").GetString();
                        var newRefresh = root.TryGetProperty("refresh_token", out var r2) ? r2.GetString() : refreshToken;
                        var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 0;

                        var auth = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        if (auth?.Succeeded == true)
                        {
                            var props = auth.Properties ?? new AuthenticationProperties();
                            var at = newAccess ?? token;
                            var rt = newRefresh ?? refreshToken;
                            var newExpires = DateTimeOffset.UtcNow.AddSeconds(expiresIn > 0 ? expiresIn : 3600);

                            var tokens = new List<AuthenticationToken>
                            {
                                new AuthenticationToken { Name = "access_token", Value = at },
                                new AuthenticationToken { Name = "refresh_token", Value = rt },
                                new AuthenticationToken { Name = "expires_at", Value = newExpires.ToString("o") }
                            };

                            props.StoreTokens(tokens);
                            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, auth.Principal!, props);

                            token = at;
                            _logger.LogInformation("Refreshed access token using refresh_token.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Refresh token request failed with status {Status}", res.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while refreshing token.");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogInformation("Attached Bearer token ({Len} chars) to {Method} {Uri}", token.Length, req.Method, req.RequestUri);
        }

        return await base.SendAsync(req, ct);
    }
}