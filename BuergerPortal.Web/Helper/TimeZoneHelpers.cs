namespace BuergerPortal.Web.Helper
{
    public static class TimeZoneHelpers
    {
        private static readonly TimeZoneInfo Berlin =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        public static DateTime ToBerlin(this DateTime utc)
            => TimeZoneInfo.ConvertTimeFromUtc(
                utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                Berlin);
    }
}
