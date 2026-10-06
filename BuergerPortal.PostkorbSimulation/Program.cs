using BuergerPortal.BundId;
using BuergerPortal.PostkorbSimulation.Api;
using BuergerPortal.PostkorbSimulation.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
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
// Nach einem Neustart der VM startet Docker die Container ohne die Reihenfolge aus Compose (depends_on); SQL Server
// fährt dann evtl. noch hoch. Bis zu 24 Versuche im Abstand von 5 s statt abzustürzen.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PostkorbDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Exception ex) when (attempt < 24)
        {
            app.Logger.LogWarning("Datenbank noch nicht erreichbar (Versuch {Attempt}/24): {Message}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

// Hinter dem Reverse Proxy (Caddy) kommt HTTP an; Schema und Client-Adresse stehen in X-Forwarded-*.
// Dem Container-Netz wird vertraut, weil der Dienst nur über den Proxy bzw. intern erreichbar ist.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

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
