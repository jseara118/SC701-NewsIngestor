using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


//comment: This class represents an item fetched from a news source, including its JSON data and metadata.
namespace SC701.NewsIngestor.Models
{
    public class SourceItem
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "ID de Fuente")]
        public int SourceId { get; set; }

        [ForeignKey(nameof(SourceId))]
        [Display(Name = "Fuente")]
        public Source Source { get; set; } = null!;

        [Required]
        [Display(Name = "Datos JSON")]
        public string Json { get; set; } = string.Empty;

        [Display(Name = "Fecha de Creación")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}