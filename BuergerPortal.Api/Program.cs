using BuergerPortal.Application;
using BuergerPortal.Application.Appointments.BusinessServices;
using BuergerPortal.Infrastructure;
using BuergerPortal.Infrastructure.Database.Persistence;
using BuergerPortal.Infrastructure.Email;
using BuergerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Services registrieren ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger Konfiguration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BürgerPortal API", Version = "v1" });
    
    // JWT Support im Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Deine Custom Services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMailJetEmailSender(builder.Configuration);
builder.Services.AddApplicationServices();

// -------------------------------------------------------
// 2. Authentication / Authorization (JWT gegen Auth-Server)
// -------------------------------------------------------
var authSection = builder.Configuration.GetSection("Authentication");
var authority = authSection["Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority is not configured.");
var audience = authSection["Audience"]
    ?? throw new InvalidOperationException("Authentication:Audience is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // OpenID Connect Authority (Discovery-Endpoint)
        options.Authority = authority;

        // In Produktion ist die Authority per HTTPS öffentlich,
        // in Dev auch, aber evtl. mit self-signed Zertifikat
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        // Claims nicht automatisch umbenennen (sub, name, role etc. bleiben wie im Token)
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // Tokens haben typischerweise einen Slash am Ende beim iss
            ValidIssuer = authority.TrimEnd('/') + "/",

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // In Dev: Self-signed Zertifikate akzeptieren (nur lokal!)
        if (builder.Environment.IsDevelopment())
        {
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }

        // E) Events für Debugging (Optional, kann später raus)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Authentication Failed: {Message}", ctx.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                return ctx.Response.WriteAsJsonAsync(new 
                { 
                    error = "unauthorized", 
                    message = "Zugriff verweigert. Token fehlt oder ungültig." 
                });
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("appointments.write", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "buergerportal_api"));
});

// --- 3. App Pipeline ---
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PortalDbContext>();

        DbInitializer.SeedAsync(context).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ein Fehler ist beim Befüllen der POI-Datenbank aufgetreten.");
    }
}

// -------------------------------------------------------------
// WICHTIG: Forwarded Headers für Nginx
// Muss VOR Authentication stehen!
// -------------------------------------------------------------
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Swagger auch in Production anzeigen (damit du testen kannst)
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// Kein HttpsRedirection (macht Nginx)
// app.UseHttpsRedirection(); 

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();