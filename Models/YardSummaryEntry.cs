using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace YardBayApp.Models
{
    [Table("yard_summary_entries")]
    public class YardSummaryEntry : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("batch_id")]
        public Guid BatchId { get; set; }

        [Column("bay_out")]
        public int BayOut { get; set; }

        [Column("recorded_at")]
        public DateTime RecordedAt { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }
    }

    /// <summary>
    /// Plain DTO used only in the UI - one row per bay while the
    /// supervisor is filling the form (not sent to Supabase directly).
    /// </summary>
    public class BayEntryInput
    {
        public string BayCode { get; set; } = string.Empty;
        public Guid BayId { get; set; }
        public int Examine { get; set; }
        public int NotExamine { get; set; }
        public int Space40ft { get; set; }
        public int Space20ft { get; set; }
    }
}
