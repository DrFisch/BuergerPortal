using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ---- (1) AccessTokenHandler für Bearer-Token an die API
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AccessTokenHandler>();

builder.Services.AddHttpClient("BuergerPortalApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7003/"); 
})
.AddHttpMessageHandler<AccessTokenHandler>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;          // "Cookies"
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;       // "oidc"
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
    {
        // UX: bei 401 automatisch zum Login
        o.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 401; return Task.CompletedTask; }
                ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask;
            }
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = "https://localhost:7001";
        options.ClientId = "mvc_web";
        options.ClientSecret = "dev_secret_very_long";
        options.ResponseType = "code";
        options.ResponseMode = "form_post";
        options.SaveTokens = true;

        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("buergerportal_api");   // <-- der API-Scope muss am AuthServer für Client mvc_web erlaubt sein

        // --- Claim-Mapping
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        // wichtig: sub -> NameIdentifier, falls du das z. B. in der API nutzt
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };

        // Dev-Quality-of-life:
        options.RequireHttpsMetadata = true; // bei https-Dev bleibt das true
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

// ---- (3) schlanke Login/Logout-Routen
app.MapGet("/login", async (HttpContext ctx) =>
{
    await ctx.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
}).AllowAnonymous();

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


/// <summary>
/// Hängt das Access Token aus dem Auth-Cookie als Bearer an alle API-Requests.
/// </summary>
public sealed class AccessTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    public AccessTokenHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var http = _accessor.HttpContext;
        var token = await http!.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(req, ct);
    }
}