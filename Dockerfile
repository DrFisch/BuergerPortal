# Basis-Image für Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Build-Image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Nur die csproj-Dateien zuerst kopieren (besseres Caching)
COPY ["BuergerPortal.Api/BuergerPortal.Api.csproj", "BuergerPortal.Api/"]
COPY ["BuergerPortal.Domain/BuergerPortal.Domain.csproj", "BuergerPortal.Domain/"]
COPY ["BuergerPortal.Application/BuergerPortal.Application.csproj", "BuergerPortal.Application/"]
COPY ["BuergerPortal.Infrastructure/BuergerPortal.Infrastructure.csproj", "BuergerPortal.Infrastructure/"]
COPY ["BuergerPortal.Infrastructure.Email/BuergerPortal.Infrastructure.Email.csproj", "BuergerPortal.Infrastructure.Email/"]

# Restore
RUN dotnet restore "BuergerPortal.Api/BuergerPortal.Api.csproj"

# Restlichen Code kopieren
COPY . .

# In API-Projekt wechseln und publishen
WORKDIR "/src/BuergerPortal.Api"
RUN dotnet publish "BuergerPortal.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Finales Image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BuergerPortal.Api.dll"]
