using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SC701.NewsIngestor.Models
{
    public class SourceItem
    {
        public int Id { get; set; }

        [Required]
        public int SourceId { get; set; }

        [ForeignKey(nameof(SourceId))]
        public Source Source { get; set; } = null!;

        [Required]
        public string Json { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
