using AuthenticationServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Server.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides; // <--- WICHTIG

var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment.EnvironmentName;
var cs = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"ENV: {env}");
Console.WriteLine($"DefaultConnection: {cs}");

// DB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseOpenIddict();
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// ---------- OpenIddict ----------
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        // Endpunkte
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetEndSessionEndpointUris("/connect/logout")
               .SetUserInfoEndpointUris("/connect/userinfo")
               .SetAccessTokenLifetime(TimeSpan.FromMinutes(60));

        // Code-Flow + PKCE
        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        options.AllowRefreshTokenFlow();

        // Scopes
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.OfflineAccess,
            "buergerportal_api"
        );

        // DEV-Zertifikate
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        var issuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
            options.SetIssuer(new Uri(issuer));

        // ASP.NET Core-Integration
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough()
               // WICHTIG: Token Passthrough ENTFERNT.
               // OpenIddict soll den Token-Request selbst verarbeiten (Engine),
               // da wir keinen eigenen Controller dafür haben.
               // .EnableTokenEndpointPassthrough()  <--- AUSKOMMENTIERT
               .DisableTransportSecurityRequirement();
        
        options.DisableAccessTokenEncryption();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.ConfigureApplicationCookie(o =>
{
    o.ExpireTimeSpan = TimeSpan.FromHours(24);
    o.SlidingExpiration = false;
    // WICHTIG: Cookie-Sicherheit für HTTPS
    o.Cookie.SameSite = SameSiteMode.None;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

var app = builder.Build();

// -------------------------------------------------------------------------
// WICHTIG: Forwarded Headers Konfiguration (Der Fix für ID2084)
// Muss GANZ OBEN stehen, bevor irgendwas anderes passiert.
// -------------------------------------------------------------------------
var forwardedOptions = new ForwardedHeadersOptions
{
    // Wir nehmen ALLES an (Proto, Host, For), um sicherzugehen, dass HTTPS erkannt wird
    ForwardedHeaders = ForwardedHeaders.All
};
// Dies ist entscheidend in Docker-Netzwerken, da die IP des Gateways sonst als "unbekannt" gilt
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedOptions);
// -------------------------------------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var config = app.Configuration;
    await SeedOpenIddictAsync(scope.ServiceProvider, config);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // app.UseHsts(); 
}

app.UseStaticFiles(); 
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();


// ---------------- SEEDING LOGIC (Unverändert) ----------------
static async Task SeedOpenIddictAsync(IServiceProvider sp, IConfiguration config)
{
    var appMgr = sp.GetRequiredService<IOpenIddictApplicationManager>();
    var scopeMgr = sp.GetRequiredService<IOpenIddictScopeManager>();

    // ---- API Scope ----
    if (await scopeMgr.FindByNameAsync("buergerportal_api") is null)
    {
        await scopeMgr.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "buergerportal_api",
            DisplayName = "BürgerPortal API scope"
        });
    }

    // ---- Client Config aus appsettings ----
    var clientSection = config.GetSection("OpenIddict:Clients:mvc_web");
    var clientSecret = clientSection["ClientSecret"];
    var redirectUris = clientSection.GetSection("RedirectUris").Get<string[]>();
    var postLogoutUris = clientSection.GetSection("PostLogoutRedirectUris").Get<string[]>();

    if (redirectUris == null || postLogoutUris == null)
        throw new InvalidOperationException("MVC redirect URIs not configured.");

    var redirectUri = new Uri(redirectUris[0]);
    var logoutUri = new Uri(postLogoutUris[0]);

    // ---- Client exists? ----
    var client = await appMgr.FindByClientIdAsync("mvc_web");

    if (client is null)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "mvc_web",
            ClientSecret = clientSecret,
            DisplayName = "BürgerPortal Web",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Prefixes.Scope + "buergerportal_api"
            },
            RedirectUris = { redirectUri },
            PostLogoutRedirectUris = { logoutUri }
        };

        await appMgr.CreateAsync(descriptor);
    }
    else
    {
        // ---- Update bestehende Werte ----
        var descriptor = new OpenIddictApplicationDescriptor();
        await appMgr.PopulateAsync(descriptor, client);

        bool changed = false;

        if (descriptor.ClientSecret != clientSecret)
        {
            descriptor.ClientSecret = clientSecret;
            changed = true;
        }

        if (!descriptor.RedirectUris.Contains(redirectUri))
        {
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.Add(redirectUri);
            changed = true;
        }

        if (!descriptor.PostLogoutRedirectUris.Contains(logoutUri))
        {
            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Add(logoutUri);
            changed = true;
        }

        if (changed)
            await appMgr.UpdateAsync(client, descriptor);
    }
}
