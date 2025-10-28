using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

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
        options.Authority = "https://localhost:7001";
        options.ClientId = "mvc_web";
        options.ClientSecret = "dev_secret_very_long";
        options.ResponseType = "code";
        options.ResponseMode = "form_post";
        options.SaveTokens = true;

        options.GetClaimsFromUserInfoEndpoint = true;   // UserInfo-Endpoint ziehen wir heran
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("buergerportal_api");

        //  Mapping: „name“ aus OIDC  ClaimTypes.Name
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

        //  Sag dem Token-Validator, welcher Claim der Anzeigename ist
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",              // <- HIER: "name"
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
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
