using BuergerPortal.Web.Features.Antraege;
using BuergerPortal.Web.Features.Termine.Contracts;
using BuergerPortal.Web.Features.Termine.ViewModels;
using BuergerPortal.Web.Helper;
using QRCoder;

namespace BuergerPortal.Web.Services;

/// <summary>Nächster eigener Termin für die Ticket-Karte (Zeiten in Berliner Zeit, Ende für den Check-in-Code in UTC).</summary>
public sealed record HeuteTermin(Guid Id, ServiceType Service, LocationType Location, DateTime StartLokal, DateTime EndLokal,
    DateTime EndUtc, Guid? AntragId);

/// <summary>Was die Startseite „Heute“ zeigt: nächster Termin, weitere Termine, offene Anträge (Entwürfe und laufende).</summary>
public sealed record HeuteDaten(HeuteTermin? NaechsterTermin, int WeitereTermine, IReadOnlyList<AntragZeileVm> OffeneAntraege,
    bool TermineFehler, bool AntraegeFehler);

/// <summary>
/// Daten der Startseite „Heute“ von Portal 2 (P2-12, E-030). Die bisherige Startseite (HomeController.Index) bleibt
/// unverändert; Portal 2 holt hier über dieselbe API zusätzlich die eigenen Anträge und den Termin samt Ende für den
/// QR-Code zum Einchecken. Beide Abfragen laufen gleichzeitig; ein Fehler zeigt nur den betroffenen Teil nicht.
/// </summary>
public sealed class ModernHeute(IHttpClientFactory clientFactory, ILogger<ModernHeute> logger)
{
    public async Task<HeuteDaten> LadenAsync(CancellationToken ct)
    {
        var client = clientFactory.CreateClient("BuergerPortalApi");
        var termine = TermineAsync(client, ct);
        var antraege = AntraegeAsync(client, ct);
        await Task.WhenAll(termine, antraege);
        var (naechster, weitere, termineFehler) = termine.Result;
        var (offen, antraegeFehler) = antraege.Result;
        return new HeuteDaten(naechster, weitere, offen, termineFehler, antraegeFehler);
    }

    private async Task<(HeuteTermin?, int, bool)> TermineAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var liste = await client.GetFromJsonAsync<List<AppointmentListItemResponse>>("api/appointments/mine", ct) ?? [];
            var kommende = liste
                .Where(t => !t.Cancelled && DateTime.SpecifyKind(t.EndUtc, DateTimeKind.Utc) > DateTime.UtcNow)
                .OrderBy(t => t.StartUtc)
                .ToList();
            var t = kommende.FirstOrDefault();
            var termin = t is null ? null : new HeuteTermin(t.Id, t.Service, t.Location, t.StartUtc.ToBerlin(), t.EndUtc.ToBerlin(),
                DateTime.SpecifyKind(t.EndUtc, DateTimeKind.Utc), t.AntragId);
            return (termin, Math.Max(0, kommende.Count - 1), false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Heute: Termine nicht geladen");
            return (null, 0, true);
        }
    }

    private async Task<(IReadOnlyList<AntragZeileVm>, bool)> AntraegeAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var alle = await EigeneAntraege.LadenAsync(client, ct);
            return alle is null ? ([], true) : (alle.Where(a => !a.IstErledigt).ToList(), false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Heute: Anträge nicht geladen");
            return ([], true);
        }
    }

    /// <summary>QR-Code als SVG vom eigenen Server (wie auf der Termin-Detailseite), skaliert per viewBox.</summary>
    public static string QrSvg(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4, "#000000", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
