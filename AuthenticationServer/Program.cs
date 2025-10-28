using AuthenticationServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Server.AspNetCore;



var builder = WebApplication.CreateBuilder(args);

// DB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseOpenIddict();
}
    );
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
               .SetUserInfoEndpointUris("/connect/userinfo");

        // Code-Flow + PKCE (für Web & MAUI)
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

        // DEV-Zertifikate (in PROD echte Zertifikate verwenden)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Optional: Issuer aus appsettings.json auslesen
        var issuer = builder.Configuration["OpenIddict:Issuer"];
        if (!string.IsNullOrWhiteSpace(issuer))
            options.SetIssuer(new Uri(issuer));

        // ASP.NET Core-Integration + Passthrough für bessere Fehlersicht
        options.UseAspNetCore().EnableAuthorizationEndpointPassthrough();

        // (Optional) Access Tokens nicht verschlüsseln – in DEV bequemer
        options.DisableAccessTokenEncryption();

        // Public Clients (MAUI) ohne ClientSecret erlauben
        //options.AllowAnonymousClients();
    })
    .AddValidation(options =>
    {
        // Falls der AuthServer selbst APIs validieren soll
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// ---------- Auth/Cookies ----------
//builder.Services.AddAuthentication()
//    .AddIdentityCookies();


builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await SeedOpenIddictAsync(scope.ServiceProvider); // <-- HIER aufrufen
    // ggf. auch SeedDevUserAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
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




static async Task SeedOpenIddictAsync(IServiceProvider sp)
{
    var appMgr = sp.GetRequiredService<IOpenIddictApplicationManager>();
    var scopeMgr = sp.GetRequiredService<IOpenIddictScopeManager>();

    // --- Scope anlegen (API) ---
    if (await scopeMgr.FindByNameAsync("buergerportal_api") is null)
    {
        await scopeMgr.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "buergerportal_api",
            DisplayName = "BürgerPortal API scope"
        });
    }

    // --- MVC Web-Client (confidential) ---
    if (await appMgr.FindByClientIdAsync("mvc_web") is null)
    {
        await appMgr.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "mvc_web",
            ClientSecret = "dev_secret_very_long", // PROD: Secret Store
            DisplayName = "BürgerPortal Web",
            ClientType = OpenIddictConstants.ClientTypes.Confidential, // <- früher: Type
            RedirectUris = { new Uri("https://localhost:7002/signin-oidc") },
            PostLogoutRedirectUris = { new Uri("https://localhost:7002/signout-callback-oidc") },
            Permissions =
            {
                // Endpunkte
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,   // <- statt "Logout"

                // Grants/Responses
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,

                // Scopes: "openid" und "offline_access" sind **special-cased**
                // und brauchen keine explizite Permission.
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Email,
                            //OpenIddictConstants.Permissions.Scopes.OfflineAccess, // <--- WICHTIG

                OpenIddictConstants.Permissions.Prefixes.Scope + "buergerportal_api"
            }
        });
    }

    // --- (Optional) MAUI (public/native) ---
    if (await appMgr.FindByClientIdAsync("maui_app") is null)
    {
        await appMgr.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "maui_app",
            DisplayName = "BürgerPortal Mobile",
            ClientType = OpenIddictConstants.ClientTypes.Public, // Public = kein Secret
            // optional hilfreich für lokale Redirects: Native-App-Type
            ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
            RedirectUris = { new Uri("buergerportal.maui://callback") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,

                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,

                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "buergerportal_api"
                // "openid" und "offline_access" -> keine explizite Permission nötig
            },
            Requirements =
            {
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
            }
        });
    }
}