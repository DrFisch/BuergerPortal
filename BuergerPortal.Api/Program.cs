// using BuergerPortal.Application;
// using BuergerPortal.Application.Appointments.BusinessServices;
// using BuergerPortal.Application.Appointments.DTOs;
// using BuergerPortal.Application.Appointments.Validation;
// using BuergerPortal.Application.Interfaces.BusinessServices;
// using BuergerPortal.Infrastructure;
// using BuergerPortal.Infrastructure.Email;
// using FluentValidation;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.IdentityModel.Tokens;

// var builder = WebApplication.CreateBuilder(args);

// // --- Services ---
// builder.Services.AddControllers();
// builder.Services.AddEndpointsApiExplorer(); // erforderlich f�r Swagger
// builder.Services.AddSwaggerGen();           // Swagger Generator
// builder.Services.AddInfrastructure(builder.Configuration);
// builder.Services.AddMailJetEmailSender(builder.Configuration);

// builder.Services.AddApplicationServices();



// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options =>
//     {
//         // 1) Authority & Discovery explizit
        
//         options.Authority = "https://localhost:7001";
//         options.MetadataAddress = "https://localhost:7001/.well-known/openid-configuration";
//         options.RequireHttpsMetadata = true;

//         // 2) Token-Validierung: Issuer/Audience exakt setzen
//         options.TokenValidationParameters = new TokenValidationParameters
//         {
//             ValidateIssuer = true,
//             ValidIssuer = "https://localhost:7001/", // <- beachte den Slash am Ende (dein Token hat den!)
//             ValidateAudience = true,
//             ValidAudience = "buergerportal_api",
//             ValidateLifetime = true,
//             ValidateIssuerSigningKey = true,
//             NameClaimType = "name",
//             RoleClaimType = "role"
//         };

//         // 3) Claims unver�ndert lassen
//         options.MapInboundClaims = false;

//         // 4) DEV: Discovery/JWKS auch mit self-signed zulassen (nur lokal!)
//         options.BackchannelHttpHandler = new HttpClientHandler
//         {
//             ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
//         };

//         // 5) Logs/ProblemDetails f�r 401/403 (hilft beim Debuggen & vermeidet leere Bodies)
//         options.Events = new JwtBearerEvents
//         {
//             OnAuthenticationFailed = ctx =>
//             {
//                 var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
//                               .CreateLogger("JWT");
//                 log.LogError(ctx.Exception, "JWT authentication failed");
//                 return Task.CompletedTask;
//             },
//             OnChallenge = ctx =>
//             {
//                 ctx.HandleResponse();
//                 ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
//                 ctx.Response.ContentType = "application/problem+json";
//                 return ctx.Response.WriteAsJsonAsync(new ProblemDetails
//                 {
//                     Title = "Unauthenticated",
//                     Detail = ctx.ErrorDescription ?? "Zugriff erfordert g�ltiges Access Token.",
//                     Status = StatusCodes.Status401Unauthorized
//                 });
//             },
//             OnForbidden = ctx =>
//             {
//                 ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
//                 ctx.Response.ContentType = "application/problem+json";
//                 return ctx.Response.WriteAsJsonAsync(new ProblemDetails
//                 {
//                     Title = "Forbidden",
//                     Detail = "Fehlende Berechtigung/Scope.",
//                     Status = StatusCodes.Status403Forbidden
//                 });
//             }
//         };
//     });

// builder.Services.AddAuthorization(options =>
// {
//     // Optional: Scope-Policy (f�r feingranulare Freigaben)
//     options.AddPolicy("appointments.write", policy =>
//         policy.RequireAuthenticatedUser()
//               .RequireClaim("scope", "buergerportal_api", "appointments.write"));
// });

// // --- App Pipeline ---
// var app = builder.Build();

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();    // erzeugt /swagger/v1/swagger.json
//     app.UseSwaggerUI();  // interaktive UI unter /swagger
// }

// app.UseHttpsRedirection();
// app.UseAuthentication();   
// app.UseAuthorization();

// app.MapControllers();

// app.Run();

using BuergerPortal.Application;
using BuergerPortal.Application.Appointments.BusinessServices; // Ggf. anpassen, falls nicht benötigt
using BuergerPortal.Infrastructure;
using BuergerPortal.Infrastructure.Email;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Für Swagger Security Definition

var builder = WebApplication.CreateBuilder(args);

// --- 1. Services registrieren ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger Konfiguration mit JWT Support (Damit du das Token im Swagger UI testen kannst)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BürgerPortal API", Version = "v1" });
    
    // Definition, dass wir Bearer Tokens nutzen
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Deine Custom Services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMailJetEmailSender(builder.Configuration);
builder.Services.AddApplicationServices();

// --- 2. AUTHENTIFIZIERUNG (JWT) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // A) Woher bekommt die API die Public Keys?
        // Bei Docker: Am besten die interne URL nutzen (z.B. http://auth-server:80), 
        // aber hier nutzen wir die Public IP, damit es konsistent zum Token ist.
        options.Authority = "http://34.89.247.235:7001";
        
        // B) HTTP erlauben (WICHTIG für dein Setup)
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;

        // C) Validierungsparameter
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://34.89.247.235:7001/", 
            
            ValidateAudience = true,
            ValidAudience = "buergerportal_api",
            
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // Da wir das Mapping oben abgeschaltet haben, müssen wir definieren,
            // welcher Claim für User.Identity.Name verwendet wird:
            NameClaimType = "name", 
            RoleClaimType = "role"
        };

        // D) Events für besseres Debugging
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
                // Verhindert den Default-Redirect und gibt JSON zurück
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
    // Dein Scope Policy
    options.AddPolicy("appointments.write", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "buergerportal_api")); // Hinweis: "appointments.write" ist oft Teil des Scopes Strings, hier einfach "buergerportal_api" prüfen reicht oft für den Anfang
});

// --- 3. App Pipeline ---
var app = builder.Build();

// Swagger auch im Release-Modus anzeigen (hilfreich für dich jetzt zum Testen)
app.UseSwagger();
app.UseSwaggerUI();

// Fehlerbehandlung
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// WICHTIG: Kein HttpsRedirection verwenden, wenn du auf HTTP (Port 80/8080) läufst!
// app.UseHttpsRedirection(); 

app.UseRouting();

// Reihenfolge wichtig: Erst AuthN (Wer bist du?), dann AuthZ (Was darfst du?)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();