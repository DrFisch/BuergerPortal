using BuergerPortal.Application;
using BuergerPortal.Application.Appointments.BusinessServices;
using BuergerPortal.Application.Appointments.DTOs;
using BuergerPortal.Application.Appointments.Validation;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Infrastructure;
using BuergerPortal.Infrastructure.Email;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // erforderlich für Swagger
builder.Services.AddSwaggerGen();           // Swagger Generator
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMailJetEmailSender(builder.Configuration);

builder.Services.AddApplicationServices();



builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // 1) Authority & Discovery explizit
        options.Authority = "https://localhost:7001";
        options.MetadataAddress = "https://localhost:7001/.well-known/openid-configuration";
        options.RequireHttpsMetadata = true;

        // 2) Token-Validierung: Issuer/Audience exakt setzen
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://localhost:7001/", // <- beachte den Slash am Ende (dein Token hat den!)
            ValidateAudience = true,
            ValidAudience = "buergerportal_api",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        // 3) Claims unverändert lassen
        options.MapInboundClaims = false;

        // 4) DEV: Discovery/JWKS auch mit self-signed zulassen (nur lokal!)
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        // 5) Logs/ProblemDetails für 401/403 (hilft beim Debuggen & vermeidet leere Bodies)
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                              .CreateLogger("JWT");
                log.LogError(ctx.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                return ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Title = "Unauthenticated",
                    Detail = ctx.ErrorDescription ?? "Zugriff erfordert gültiges Access Token.",
                    Status = StatusCodes.Status401Unauthorized
                });
            },
            OnForbidden = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/problem+json";
                return ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = "Fehlende Berechtigung/Scope.",
                    Status = StatusCodes.Status403Forbidden
                });
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Optional: Scope-Policy (für feingranulare Freigaben)
    options.AddPolicy("appointments.write", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "buergerportal_api", "appointments.write"));
});

// --- App Pipeline ---
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // erzeugt /swagger/v1/swagger.json
    app.UseSwaggerUI();  // interaktive UI unter /swagger
}

app.UseHttpsRedirection();
app.UseAuthentication();   
app.UseAuthorization();

app.MapControllers();

app.Run();
