using AuthenticationServer.BundId;
using BuergerPortal.BundId;
using AuthenticationServer.Controllers;
using AuthenticationServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Server.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Cryptography.X509Certificates;
var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment.EnvironmentName;
var cs = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"ENV: {env}");
// Nur Server und Datenbank ausgeben – nie Benutzer oder Passwort (die Ausgabe landet z. B. in "docker logs").
Console.WriteLine($"DefaultConnection: {DescribeConnection(cs)}");

static string DescribeConnection(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString)) return "(nicht konfiguriert)";
    try
    {
        var parts = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        return $"Server={parts.DataSource}; Database={parts.InitialCatalog}";
    }
    catch (ArgumentException)
    {
        return "(ungültige Verbindungszeichenfolge)";
    }
}

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
            "buergerportal_api",
            // BundID-Daten der Person (Name, Geburtsdatum, Adresse, Niveau, Postkorb-Handle …)
            AuthorizationController.BundIdScope
        );

        // Schlüssel für Signatur und Verschlüsselung der Tokens. Im Betrieb aus PFX-Dateien (Volume), damit
        // ausgestellte Tokens einen Neustart des Containers überstehen; ohne Konfiguration (lokal)
        // Entwicklungszertifikate aus dem Zertifikatspeicher des Benutzers.
        var signingCertificatePath = builder.Configuration["OpenIddict:SigningCertificatePath"];
        var encryptionCertificatePath = builder.Configuration["OpenIddict:EncryptionCertificatePath"];
        if (!string.IsNullOrWhiteSpace(signingCertificatePath) && !string.IsNullOrWhiteSpace(encryptionCertificatePath))
        {
            var certificatePassword = builder.Configuration["OpenIddict:CertificatePassword"];
            options.AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                       signingCertificatePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet))
                   .AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                       encryptionCertificatePath, certificatePassword, X509KeyStorageFlags.EphemeralKeySet));
        }
        else
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }

        var issuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
            options.SetIssuer(new Uri(issuer));

        // ASP.NET Core-Integration
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough()

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
    o.Cookie.SameSite = SameSiteMode.None;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Ohne Sitzung geht es zur Anmeldung über die BundID (statt zur Passwort-Seite von Identity).
    o.LoginPath = "/bundid/login";
});

// ---------- BundID (SAML) ----------
builder.Services.AddBundIdServiceProvider(builder.Configuration);
builder.Services.AddScoped<BundIdUserService>();
// Die BundID-Claims stecken nur in der Sitzung (nicht in der DB). Bei der regelmäßigen Prüfung des
// Security-Stamps baut Identity die Sitzung neu auf – dabei die BundID-Claims übernehmen.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.OnRefreshingPrincipal = context =>
{
    var newIdentity = context.NewPrincipal?.Identities.FirstOrDefault();
    var bundIdClaims = context.CurrentPrincipal?.Claims.Where(c => BundIdClaimTypes.All.Contains(c.Type)) ?? [];
    newIdentity?.AddClaims(bundIdClaims.Where(c => !newIdentity.HasClaim(c.Type, c.Value)));
    return Task.CompletedTask;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

var app = builder.Build();


var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.All
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedOptions);

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

// Anmeldung nur über die BundID: Von der Identity-Oberfläche bleiben nur Login (Hinweis + BundID-Button),
// Logout und AccessDenied. Registrierung, Passwort-Funktionen und Kontoverwaltung führen zur Anmeldeseite.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Identity/Account", out var page)
        && !page.StartsWithSegments("/Login")
        && !page.StartsWithSegments("/Logout")
        && !page.StartsWithSegments("/AccessDenied"))
    {
        context.Response.Redirect("/Identity/Account/Login");
        return;
    }
    await next();
});

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

    var clientSection = config.GetSection("OpenIddict:Clients:mvc_web");
    var clientSecret = clientSection["ClientSecret"];
    var redirectUris = clientSection.GetSection("RedirectUris").Get<string[]>();
    var postLogoutUris = clientSection.GetSection("PostLogoutRedirectUris").Get<string[]>();

    if (redirectUris == null || postLogoutUris == null)
        throw new InvalidOperationException("MVC redirect URIs not configured.");

    var redirectUri = new Uri(redirectUris[0]);
    var logoutUri = new Uri(postLogoutUris[0]);

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
                OpenIddictConstants.Permissions.Prefixes.Scope + "buergerportal_api",
                OpenIddictConstants.Permissions.Prefixes.Scope + AuthorizationController.BundIdScope
            },
            RedirectUris = { redirectUri },
            PostLogoutRedirectUris = { logoutUri }
        };

        await appMgr.CreateAsync(descriptor);
    }
    else
    {
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

        // Bestehende Clients bekommen die Berechtigung für den Scope "bundid" nachträglich.
        var bundIdScopePermission = OpenIddictConstants.Permissions.Prefixes.Scope + AuthorizationController.BundIdScope;
        if (!descriptor.Permissions.Contains(bundIdScopePermission))
        {
            descriptor.Permissions.Add(bundIdScopePermission);
            changed = true;
        }

        if (changed)
            await appMgr.UpdateAsync(client, descriptor);
    }
}
