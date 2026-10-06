using BuergerPortal.PostkorbSimulation.Api;
using BuergerPortal.PostkorbSimulation.Data;
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
