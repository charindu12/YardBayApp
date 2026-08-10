using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace YardBayApp.Models
{
    /// <summary>
    /// "Total Gate In and Out Container" is kept separate from bay_status_entries /
    /// yard_summary_entries on purpose: containers that move on a given day can often
    /// only be counted/confirmed the next day, so the supervisor needs to pick a
    /// DIFFERENT date for this than the date used for the bay entry form above.
    /// One row per gate_date (re-saving the same date overwrites it, both In and Out
    /// together since they share the same date).
    /// </summary>
    [Table("gate_in_out_entries")]
    public class GateInOutEntry : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("gate_date")]
        public DateTime GateDate { get; set; }

        [Column("total_gate_in")]
        public int TotalGateIn { get; set; }

        [Column("total_gate_out")]
        public int TotalGateOut { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// Links this gate entry to the SPECIFIC bay batch (batch_id shared by that
        /// submission's 4 bay rows + yard summary row) it was saved alongside. Used by
        /// the dashboard to show "whatever gate numbers were entered together with THIS
        /// bay submission" - independent of gate_date and independent of real-world save
        /// timing, so it never gets confused when multiple submissions are saved close
        /// together (e.g. testing or back-filling several days at once).
        /// </summary>
        [Column("bay_batch_id")]
        public Guid? BayBatchId { get; set; }
    }
}
