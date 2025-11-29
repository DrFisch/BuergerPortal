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
    o.Cookie.SameSite = SameSiteMode.Lax;
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

    if (await scopeMgr.FindByNameAsync("buergerportal_api") is null)
    {
        await scopeMgr.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "buergerportal_api",
            DisplayName = "BürgerPortal API scope"
        });
    }

    var mvcRedirectUri = new Uri("https://portal.gortisbuergerportal.de/signin-oidc");
    var mvcLogoutUri = new Uri("https://portal.gortisbuergerportal.de/signout-callback-oidc");

    var client = await appMgr.FindByClientIdAsync("mvc_web");

    if (client is null)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "mvc_web",
            ClientSecret = "HalloGort123!", 
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
            RedirectUris = { mvcRedirectUri },
            PostLogoutRedirectUris = { mvcLogoutUri }
        };
        await appMgr.CreateAsync(descriptor);
    }
    else
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await appMgr.PopulateAsync(descriptor, client);
        
        if (!descriptor.RedirectUris.Contains(mvcRedirectUri))
        {
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.Add(mvcRedirectUri);
            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.Add(mvcLogoutUri);
            await appMgr.UpdateAsync(client, descriptor);
        }
    }
}