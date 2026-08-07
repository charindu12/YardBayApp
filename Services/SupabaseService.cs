using Supabase;
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
                AutoConnectRealtime = true
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
        /// Saves one full submission: 4 bay rows + 1 yard summary row,
        /// all sharing the same batch_id so they can be grouped later.
        /// </summary>
        public async Task SaveBatchAsync(List<BayEntryInput> bayInputs, int bayOut, DateTime recordedAt, Guid? userId)
        {
            var client = await GetClientAsync();
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
