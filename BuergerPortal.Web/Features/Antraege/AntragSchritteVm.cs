namespace BuergerPortal.Web.Features.Antraege
{
    /// <summary>Schrittanzeige der Antrags-Assistenten: Namen der Schritte (schon übersetzt) und der aktive (ab 1).</summary>
    public sealed record AntragSchritteVm(IReadOnlyList<string> Namen, int Aktiv);
}
