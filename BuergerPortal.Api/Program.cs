using BuergerPortal.Application.Appointments.BusinessServices;
using BuergerPortal.Application.Interfaces.BusinessServices;
using BuergerPortal.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // erforderlich für Swagger
builder.Services.AddSwaggerGen();           // Swagger Generator
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IAppointmentBusinessService, AppointmentBusinessService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];   // z.B. https://auth.example.com
        options.Audience = "buergerportal_api";                       // deine API-Ressource
        options.RequireHttpsMetadata = true;                            // dev ggf. false

        // Optional, aber hilfreich für saubere Claims:
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "name",                    // oder ClaimTypes.Name
            RoleClaimType = "role"                     // falls Rollen genutzt werden
        };

        // Inbound Claim Mapping ausschalten, wenn du „rohe“ Claim-Namen willst:
        options.MapInboundClaims = false;
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
