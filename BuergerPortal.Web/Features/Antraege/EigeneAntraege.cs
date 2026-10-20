using BuergerPortal.Web.Features.Antraege.Meldebescheinigung.Contracts;
using BuergerPortal.Web.Features.Antraege.Reisepass.Contracts;
using BuergerPortal.Web.Features.Antraege.Sperrmuell.Contracts;
using BuergerPortal.Web.Helper;
using System.Net;

namespace BuergerPortal.Web.Features.Antraege
{
    /// <summary>
    /// Eigene Anträge aller Arten (Reisepass, Sperrmüll, Meldebescheinigung) aus der API als Zeilen, neueste zuerst –
    /// für „Anträge“, „Meine Anträge“ (AntraegeController) und die Startseite „Heute“ von Portal 2. Die API liefert nur
    /// die Anträge der angemeldeten Person (bPK2 aus dem Access-Token). null bei 401: Sitzung abgelaufen → neu anmelden.
    /// </summary>
    public static class EigeneAntraege
    {
        public static async Task<List<AntragZeileVm>?> LadenAsync(HttpClient client, CancellationToken ct)
        {
            var zeilen = new List<AntragZeileVm>();

            var pass = await client.GetAsync("api/antraege/reisepass", ct);
            if (pass.StatusCode == HttpStatusCode.Unauthorized) return null;
            if (pass.IsSuccessStatusCode)
            {
                zeilen.AddRange((await pass.Content.ReadFromJsonAsync<List<ReisepassSummaryResponse>>(cancellationToken: ct) ?? [])
                    .Select(x => Zeile(x.Id, x.Typ, x.Status, x.Vorname, x.Nachname, x.CreatedUtc, x.SubmittedUtc)));
            }

            var sperr = await client.GetAsync("api/antraege/sperrmuell", ct);
            if (sperr.StatusCode == HttpStatusCode.Unauthorized) return null;
            if (sperr.IsSuccessStatusCode)
            {
                zeilen.AddRange((await sperr.Content.ReadFromJsonAsync<List<SperrmuellSummaryResponse>>(cancellationToken: ct) ?? [])
                    .Select(x => Zeile(x.Id, x.Typ, x.Status, x.Vorname, x.Nachname, x.CreatedUtc, x.SubmittedUtc)));
            }

            var melde = await client.GetAsync("api/antraege/meldebescheinigung", ct);
            if (melde.StatusCode == HttpStatusCode.Unauthorized) return null;
            if (melde.IsSuccessStatusCode)
            {
                zeilen.AddRange((await melde.Content.ReadFromJsonAsync<List<MeldebescheinigungSummaryResponse>>(cancellationToken: ct) ?? [])
                    .Select(x => Zeile(x.Id, x.Typ, x.Status, x.Vorname, x.Nachname, x.CreatedUtc, x.SubmittedUtc)));
            }

            return zeilen.OrderByDescending(z => z.Zeitpunkt).ToList();
        }

        private static AntragZeileVm Zeile(Guid id, int typ, int status, string? vorname, string? nachname, DateTime angelegt,
            DateTime? eingereicht) => new()
        {
            Id = id,
            Typ = typ,
            Status = status,
            Antragsteller = $"{vorname} {nachname}".Trim(),
            Angelegt = angelegt.ToBerlin(),
            Eingereicht = eingereicht?.ToBerlin(),
        };
    }
}
