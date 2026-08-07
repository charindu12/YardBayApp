using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace YardBayApp.Models
{
    [Table("bays")]
    public class Bay : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;   // "A","B","C","D"

        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
