using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;          // "Cookies"
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;       // "oidc"
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = "https://localhost:7001";   // AuthServer-URL
        options.ClientId = "mvc_web";
        options.ClientSecret = "dev_secret_very_long";  // wie im Seeding
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        options.Scope.Add("openid");            // special-cased, muss hier aber angefordert werden
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("buergerportal_api"); // dein API-Scope
    });

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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
