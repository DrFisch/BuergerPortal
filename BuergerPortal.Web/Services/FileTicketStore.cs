using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace BuergerPortal.Web.Services;

/// <summary>
/// Sitzungsdaten (Claims aus der BundID, Tokens) auf dem Server statt im Cookie: Das Cookie enthält nur noch eine
/// zufällige Sitzungskennung. Vorher wuchs das Cookie mit Refresh-Token auf rund 12 KB (drei Teile) – zu groß für
/// Proxys mit 8-KB-Grenze je Header-Zeile. Außerdem löscht das Abmelden die Sitzung auch auf dem Server.
/// Je Sitzung eine Datei, verschlüsselt mit Data Protection (im Container im Volume der Schlüssel, übersteht also
/// Neustarts). Abgelaufene Dateien werden regelmäßig entfernt.
/// </summary>
public sealed class FileTicketStore : ITicketStore
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
    private readonly DirectoryInfo _directory;
    private readonly IDataProtector _protector;
    private readonly ILogger<FileTicketStore> _logger;
    private DateTime _lastCleanupUtc = DateTime.MinValue;

    public FileTicketStore(string directory, IDataProtectionProvider dataProtection, ILogger<FileTicketStore> logger)
    {
        _directory = Directory.CreateDirectory(directory);
        _protector = dataProtection.CreateProtector("BuergerPortal.Web.Sitzungen");
        _logger = logger;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await RenewAsync(key, ticket);
        RemoveExpired();
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (!IsValidKey(key))
        {
            return;
        }
        var data = _protector.Protect(TicketSerializer.Default.Serialize(ticket));
        // Erst in eine eigene Datei schreiben, dann ersetzen: gleichzeitige Anfragen sehen nie eine halbe Datei.
        var tmp = Path.Combine(_directory.FullName, $"{key}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(tmp, data);
        File.Move(tmp, FileOf(key), overwrite: true);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!IsValidKey(key) || !File.Exists(FileOf(key)))
        {
            return null;
        }
        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(await File.ReadAllBytesAsync(FileOf(key))));
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            _logger.LogWarning(ex, "Sitzung nicht lesbar – wird verworfen.");
            return null;
        }
    }

    public Task RemoveAsync(string key)
    {
        if (IsValidKey(key))
        {
            File.Delete(FileOf(key));
        }
        return Task.CompletedTask;
    }

    // Die Kennung kommt aus dem (geschützten) Cookie; trotzdem nur Hex-Zeichen zulassen – kein Pfad aus Eingaben.
    private static bool IsValidKey(string key) => key.Length == 64 && key.All(Uri.IsHexDigit);

    private string FileOf(string key) => Path.Combine(_directory.FullName, key + ".ticket");

    // Dateien, die länger als die Höchstdauer einer Sitzung nicht erneuert wurden, sind sicher abgelaufen.
    private void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanupUtc < CleanupInterval)
        {
            return;
        }
        _lastCleanupUtc = now;
        foreach (var file in _directory.EnumerateFiles("*.ticket").Concat(_directory.EnumerateFiles("*.tmp")))
        {
            if (now - file.LastWriteTimeUtc > PortalSession.MaxLifetime + TimeSpan.FromHours(1))
            {
                try { file.Delete(); } catch (IOException) { /* gerade in Benutzung – beim nächsten Mal */ }
            }
        }
    }
}

/// <summary>Verdrahtung: SessionStore des Cookie-Schemas (braucht Data Protection aus dem DI-Container).</summary>
public static class FileTicketStoreExtensions
{
    public static IServiceCollection AddFileTicketStore(this IServiceCollection services, string directory)
    {
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IDataProtectionProvider, ILoggerFactory>((options, dataProtection, loggers) =>
                options.SessionStore = new FileTicketStore(directory, dataProtection, loggers.CreateLogger<FileTicketStore>()));
        return services;
    }
}
