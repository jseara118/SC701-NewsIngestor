using System.ComponentModel.DataAnnotations;

namespace SC701.NewsIngestor.Models
{
    public class Source
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(100)]
        public string ComponentType { get; set; } = string.Empty;

        public bool RequiresSecret { get; set; }
    }
}
