namespace BuergerPortal.Web.Services
{
    /// <summary>
    /// Zugriff auf die angemeldete Person für Controller und Views ("Meine Daten", Navigation, Vorbefüllen von
    /// Formularen). Bündelt das Lesen der BundID-Claims an einer Stelle.
    /// </summary>
    public sealed class HttpCurrentUserService(IHttpContextAccessor accessor)
    {
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        /// <summary>BundID-Angaben der angemeldeten Person; null ohne Anmeldung.</summary>
        public BundIdUser? GetBundIdUser() =>
            IsAuthenticated ? BundIdUser.FromPrincipal(accessor.HttpContext!.User) : null;
    }
}
