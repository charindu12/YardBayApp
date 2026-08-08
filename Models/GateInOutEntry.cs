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
    }
}
