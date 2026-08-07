using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace YardBayApp.Models
{
    [Table("bay_status_entries")]
    public class BayStatusEntry : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("batch_id")]
        public Guid BatchId { get; set; }

        [Column("bay_id")]
        public Guid BayId { get; set; }

        [Column("examine_count")]
        public int ExamineCount { get; set; }

        [Column("not_examine_count")]
        public int NotExamineCount { get; set; }

        [Column("space_40ft")]
        public int Space40ft { get; set; }

        [Column("space_20ft")]
        public int Space20ft { get; set; }

        [Column("recorded_at")]
        public DateTime RecordedAt { get; set; }

        [Column("created_by")]
        public Guid? CreatedBy { get; set; }
    }
}
