using BuergerPortal.BundId;
using BuergerPortal.PostkorbSimulation.Api;
using BuergerPortal.PostkorbSimulation.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection fehlt.");
builder.Services.AddDbContext<PostkorbDbContext>(options => options.UseSqlServer(connectionString));

// REST-Schnittstelle: Start bricht ab, wenn kein ausreichend langer API-Schlüssel konfiguriert ist.
builder.Services.AddOptions<PostkorbApiOptions>()
    .Bind(builder.Configuration.GetSection(PostkorbApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);

// Oberfläche: Anmeldung über die BundID als eigener SAML-Service-Provider, danach Cookie-Sitzung.
builder.Services.AddBundIdServiceProvider(builder.Configuration);

// Schlüssel für Sitzungs-Cookie und Anmeldezustand (bundid_login). Im Container in einem Volume
// (DataProtection:KeysPath), sonst wären nach jedem Neustart alle Sitzungen ungültig.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("BuergerPortal.PostkorbSimulation")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        // Eigener Name: Auf localhost teilen sich alle Dienste die Cookies (Ports trennen Cookies nicht).
        o.Cookie.Name = "bpsim_postfach";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.LoginPath = "/bundid/login";
        o.ReturnUrlParameter = "returnUrl";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
    });

var app = builder.Build();

// Im Container (Database:MigrateOnStartup=true) legt der Dienst seine Datenbank PostkorbDB selbst an bzw. aktualisiert sie.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PostkorbDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
