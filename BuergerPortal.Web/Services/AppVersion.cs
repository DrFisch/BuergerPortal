using System.Reflection;

namespace BuergerPortal.Web.Services
{
    /// <summary>
    /// Versionsanzeige in der Fußzeile, z. B. „1.1 (f44a706)“: Nummer aus <c>Directory.Build.props</c> (eine Stelle für
    /// alle Projekte), Commit aus dem Build – das SDK schreibt beides als „1.1+&lt;commit&gt;“ in die
    /// AssemblyInformationalVersion (lokal aus Git, im Container aus dem Build-Argument von deploy.ps1). So lässt sich die
    /// laufende Version einem Stand auf GitHub zuordnen.
    /// </summary>
    public static class AppVersion
    {
        public static string Display { get; } = Format(
            typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        /// <summary>„1.1+&lt;commit&gt;“ → „1.1 (&lt;7 Zeichen&gt;)“; ohne Commit nur die Nummer.</summary>
        public static string Format(string? informationalVersion)
        {
            var parts = (informationalVersion ?? "").Split('+', 2, StringSplitOptions.TrimEntries);
            var number = parts[0].Length > 0 ? parts[0] : "?";
            return parts.Length > 1 && parts[1].Length > 0
                ? $"{number} ({parts[1][..Math.Min(7, parts[1].Length)]})"
                : number;
        }
    }
}
