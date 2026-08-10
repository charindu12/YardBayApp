namespace YardBayApp.Helpers
{
    /// <summary>
    /// Sri Lanka is a fixed UTC+5:30 offset (no daylight saving).
    /// Use this everywhere instead of DateTime.Now / ToLocalTime(),
    /// so the result never depends on the phone's or PC's system timezone.
    /// </summary>
    public static class SriLankaTime
    {
        public static readonly TimeSpan Offset = TimeSpan.FromHours(5.5);

        /// <summary>
        /// Takes a "wall clock" date+time the supervisor picked (meant as Sri Lanka
        /// local time) and converts it to a proper UTC DateTime ready to save to Supabase.
        /// </summary>
        public static DateTime ToUtc(DateTime slLocalDateTime)
        {
            var unspecified = DateTime.SpecifyKind(slLocalDateTime, DateTimeKind.Unspecified);
            var withOffset = new DateTimeOffset(unspecified, Offset);
            return withOffset.UtcDateTime; // Kind = Utc, safe to serialize
        }

        /// <summary>
        /// Converts a UTC DateTime (e.g. DateTime.UtcNow) to Sri Lanka wall-clock time,
        /// for display purposes only (e.g. the live clock on the entry page).
        /// </summary>
        public static DateTime ToLocal(DateTime utcDateTime)
        {
            // The Supabase/Postgrest JSON client can hand back a DateTime whose Kind is
            // Local (auto-shifted to this machine's system timezone during deserialize)
            // or Unspecified, not always Utc. Blindly stamping Kind=Utc on an
            // already-shifted Local value and then adding the SL offset caused a
            // double-shift bug (e.g. 08:15 showing as 13:45). Normalize to true UTC
            // first, based on whatever Kind we actually got, then add the offset once.
            DateTime trueUtc = utcDateTime.Kind switch
            {
                DateTimeKind.Utc => utcDateTime,
                DateTimeKind.Local => utcDateTime.ToUniversalTime(),
                _ => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
            };
            return trueUtc.Add(Offset);
        }
    }
}
