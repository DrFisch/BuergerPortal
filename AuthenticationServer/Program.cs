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
               .SetUserInfoEndpointUris("/connect/userinfo").SetAccessTokenLifetime(TimeSpan.FromMinutes(60));

        // Code-Flow + PKCE (f�r Web & MAUI)
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

        // ASP.NET Core-Integration + Passthrough f�r bessere Fehlersicht
        options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableEndSessionEndpointPassthrough();
        options.UseAspNetCore().DisableTransportSecurityRequirement();
        // (Optional) Access Tokens nicht verschl�sseln � in DEV bequemer
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

builder.Services.ConfigureApplicationCookie(o =>
{
    o.ExpireTimeSpan = TimeSpan.FromHours(24);
    o.SlidingExpiration = false;
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
    var config = app.Configuration; // oder scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await SeedOpenIddictAsync(scope.ServiceProvider, config);
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
    //app.UseHsts();
}

//app.UseHttpsRedirection();
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

    // --- Scope für API ---
    if (await scopeMgr.FindByNameAsync("buergerportal_api") is null)
    {
        await scopeMgr.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "buergerportal_api",
            DisplayName = "BürgerPortal API scope"
        });
    }

    // ---- MVC Web-Client (mvc_web) ----
    if (await appMgr.FindByClientIdAsync("mvc_web") is null)
{
        // Hier hart codieren:
        var redirectUri = new Uri("http://34.89.247.235:5001/signin-oidc");
        var postLogoutUri = new Uri("http://34.89.247.235:5001/signout-callback-oidc");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "mvc_web",
            ClientSecret = "HalloGort123!", // oder production_secret – Hauptsache identisch mit MVC
            DisplayName = "BürgerPortal Web",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions =
            {
                // Endpoints
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,

                // Grant types
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                // Response types
                OpenIddictConstants.Permissions.ResponseTypes.Code,

                // Scopes
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Prefixes.Scope + "buergerportal_api"
            },

            RedirectUris = { redirectUri },
            PostLogoutRedirectUris = { postLogoutUri }
        };

        await appMgr.CreateAsync(descriptor);
    }
}
