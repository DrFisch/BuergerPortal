using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Net;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");

// MVC + View/DataAnnotations-Lokalisierung
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

    // Wichtig: Cookie zuerst, dann QueryString, dann Browser
    opts.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider(),        // liest dein Culture-Cookie
        new QueryStringRequestCultureProvider(),   // optional ?culture=en
        new AcceptLanguageHeaderRequestCultureProvider()
    };

    opts.SetDefaultCulture("de");
});

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
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        o.ExpireTimeSpan = TimeSpan.FromHours(24);  // harte 24h
        o.SlidingExpiration = false;                // nicht verlängern
        o.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 401; return Task.CompletedTask; }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
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
        options.Scope.Add("buergerportal_api");

        // Nach 24h IdP-seitig wirklich neu einloggen
        options.MaxAge = TimeSpan.FromHours(24);

        // kürzere Timeouts, damit „Auth down“ nicht lange blockiert
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
            // Wenn Authority nicht erreichbar: freundlich abbiegen statt Exception
            OnRedirectToIdentityProvider = async ctx =>
            {
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
            // UI-Cookie-Lebensdauer hier explizit setzen (24h hart)
            OnTokenValidated = ctx =>
            {
                ctx.Properties.IsPersistent = false;
                ctx.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24);
                return Task.CompletedTask;
            }
        };
    });



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
            // WICHTIG: Backchannel NICHT disposen – er gehört dem OIDC-Handler!
            using var req = new HttpRequestMessage(HttpMethod.Get, wellKnown);
            using var res = await opts.Backchannel.SendAsync(req, cts.Token);
            return res.IsSuccessStatusCode;
        }

        // Nur den selbst erstellten Client disposen
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var locOptions = app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseRouting();

app.UseAuthentication();

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
    // Lokales Cookie immer löschen
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // OIDC-Logout nur, wenn Authority erreichbar
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
