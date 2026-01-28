using System.ComponentModel.DataAnnotations;

namespace SC701.NewsIngestor.Models
{
    public class Source
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La URL es obligatoria")]
        [MaxLength(500)]
        [Url(ErrorMessage = "Debe ser una URL válida")]
        [Display(Name = "URL de la fuente")]
        public string Url { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [MaxLength(200)]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "El tipo de componente es obligatorio")]
        [MaxLength(100)]
        [Display(Name = "Tipo de Componente")]
        public string ComponentType { get; set; } = string.Empty;

        [Display(Name = "¿Requiere Secret?")]
        public bool RequiresSecret { get; set; }

        // Relación con SourceItems
        public virtual ICollection<SourceItem>? SourceItems { get; set; }
    }
}