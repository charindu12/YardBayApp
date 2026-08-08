using Supabase;
using YardBayApp.Helpers;
using YardBayApp.Models;

namespace YardBayApp.Services
{
    public class SupabaseService
    {
        // TODO: replace with your project's URL and anon (public) key
        // Supabase Dashboard > Project Settings > API
        private const string SupabaseUrl = "https://ootwjzuquhgggkzexatv.supabase.co";
        private const string SupabaseAnonKey = "sb_publishable_XSjwuI4f6YgAwtAjnOL7xg_ZRbPF4wG";

        private Supabase.Client? _client;
        public static SupabaseService Instance { get; } = new SupabaseService();

        private SupabaseService() { }

        public async Task<Supabase.Client> GetClientAsync()
        {
            if (_client != null) return _client;

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = false
            };

            _client = new Supabase.Client(SupabaseUrl, SupabaseAnonKey, options);
            await _client.InitializeAsync();
            return _client;
        }

        /// <summary>Loads the 4 bays, ordered A-D.</summary>
        public async Task<List<Bay>> GetBaysAsync()
        {
            var client = await GetClientAsync();
            var result = await client.From<Bay>()
                .Order("sort_order", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();
            return result.Models;
        }

        /// <summary>
        /// Saves one full submission: 4 bay rows + 1 yard summary row, all sharing the
        /// same batch_id. If a submission already exists for the same Sri Lanka calendar
        /// day (e.g. the supervisor is correcting today's entry by picking today's date
        /// again, or fixing a past day by picking that date), the old submission for that
        /// day is removed first so this acts as an edit/overwrite instead of stacking a
        /// duplicate on top of it.
        /// </summary>
        public async Task SaveOrUpdateBatchAsync(List<BayEntryInput> bayInputs, int bayOut, DateTime recordedAt, Guid? userId)
        {
            var client = await GetClientAsync();

            // recordedAt is already UTC (converted by the caller). Work out which
            // Sri Lanka calendar day it falls on, then the UTC bounds of that day.
            var slDate = SriLankaTime.ToLocal(recordedAt).Date;
            var dayStartUtc = SriLankaTime.ToUtc(slDate);
            var dayEndUtc = SriLankaTime.ToUtc(slDate.AddDays(1).AddMilliseconds(-1));

            var existingSummaries = await client.From<YardSummaryEntry>()
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, dayStartUtc.ToString("o"))
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, dayEndUtc.ToString("o"))
                .Get();

            // Remove any submission(s) already stored for that day before inserting the new one.
            foreach (var old in existingSummaries.Models)
            {
                await client.From<BayStatusEntry>()
                    .Filter("batch_id", Supabase.Postgrest.Constants.Operator.Equals, old.BatchId.ToString())
                    .Delete();

                await client.From<YardSummaryEntry>()
                    .Filter("batch_id", Supabase.Postgrest.Constants.Operator.Equals, old.BatchId.ToString())
                    .Delete();
            }

            var batchId = Guid.NewGuid();

            var rows = bayInputs.Select(b => new BayStatusEntry
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                BayId = b.BayId,
                ExamineCount = b.Examine,
                NotExamineCount = b.NotExamine,
                Space40ft = b.Space40ft,
                Space20ft = b.Space20ft,
                RecordedAt = recordedAt,
                CreatedBy = userId
            }).ToList();

            await client.From<BayStatusEntry>().Insert(rows);

            var summary = new YardSummaryEntry
            {
                Id = Guid.NewGuid(),
                BatchId = batchId,
                BayOut = bayOut,
                RecordedAt = recordedAt,
                CreatedBy = userId
            };

            await client.From<YardSummaryEntry>().Insert(summary);
        }

        /// <summary>
        /// Saves the "Total Gate In and Out Container" numbers for a specific gate date.
        /// This date is independent of whatever date the bay entry form is showing
        /// (containers that move today are usually only confirmed/counted tomorrow),
        /// so it has its own table keyed one-row-per-date. Re-saving the same date
        /// overwrites the previous In/Out values instead of duplicating them.
        /// </summary>
        public async Task SaveOrUpdateGateInOutAsync(DateTime gateDate, int totalIn, int totalOut, Guid? userId)
        {
            var client = await GetClientAsync();
            var dateOnly = gateDate.Date;

            var existing = await client.From<GateInOutEntry>()
                .Filter("gate_date", Supabase.Postgrest.Constants.Operator.Equals, dateOnly.ToString("yyyy-MM-dd"))
                .Get();

            foreach (var old in existing.Models)
            {
                await client.From<GateInOutEntry>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, old.Id.ToString())
                    .Delete();
            }

            var entry = new GateInOutEntry
            {
                Id = Guid.NewGuid(),
                GateDate = dateOnly,
                TotalGateIn = totalIn,
                TotalGateOut = totalOut,
                CreatedBy = userId
            };

            await client.From<GateInOutEntry>().Insert(entry);
        }

        /// <summary>Looks up the gate in/out numbers already saved for a given date, if any -
        /// so the fields can be pre-filled when the supervisor picks that date.</summary>
        public async Task<GateInOutEntry?> GetGateInOutForDateAsync(DateTime gateDate)
        {
            var client = await GetClientAsync();
            var dateOnly = gateDate.Date;

            var result = await client.From<GateInOutEntry>()
                .Filter("gate_date", Supabase.Postgrest.Constants.Operator.Equals, dateOnly.ToString("yyyy-MM-dd"))
                .Get();

            return result.Models.FirstOrDefault();
        }

        /// <summary>
        /// Looks up whatever was already saved for the given Sri Lanka calendar day (if
        /// anything), so the entry form can be pre-filled when the supervisor picks that
        /// date - instead of showing blank/0 fields and risking overwriting real numbers
        /// with zeros on save.
        /// </summary>
        public async Task<(List<BayStatusEntry> BayRows, YardSummaryEntry? Summary)> GetEntryForDateAsync(DateTime slLocalDate)
        {
            var client = await GetClientAsync();

            var dayStartUtc = SriLankaTime.ToUtc(slLocalDate.Date);
            var dayEndUtc = SriLankaTime.ToUtc(slLocalDate.Date.AddDays(1).AddMilliseconds(-1));

            var summaryResult = await client.From<YardSummaryEntry>()
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, dayStartUtc.ToString("o"))
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, dayEndUtc.ToString("o"))
                .Get();

            var summary = summaryResult.Models.FirstOrDefault();
            if (summary == null)
                return (new List<BayStatusEntry>(), null);

            var bayResult = await client.From<BayStatusEntry>()
                .Filter("batch_id", Supabase.Postgrest.Constants.Operator.Equals, summary.BatchId.ToString())
                .Get();

            return (bayResult.Models, summary);
        }

        /// <summary>Fetches batches between two dates (inclusive), newest first.</summary>
        public async Task<List<BayStatusEntry>> GetHistoryAsync(DateTime fromDate, DateTime toDate)
        {
            var client = await GetClientAsync();
            var result = await client.From<BayStatusEntry>()
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, fromDate.ToString("o"))
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, toDate.ToString("o"))
                .Order("recorded_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();
            return result.Models;
        }

        /// <summary>Fetches the Bay Out (yard_summary_entries) rows for the same date range -
        /// paired with GetHistoryAsync's bay rows by BatchId to build one card per submission.</summary>
        public async Task<List<YardSummaryEntry>> GetYardSummaryHistoryAsync(DateTime fromDate, DateTime toDate)
        {
            var client = await GetClientAsync();
            var result = await client.From<YardSummaryEntry>()
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, fromDate.ToString("o"))
                .Filter("recorded_at", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, toDate.ToString("o"))
                .Order("recorded_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();
            return result.Models;
        }
    }
}
