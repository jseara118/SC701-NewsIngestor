using System.ComponentModel.DataAnnotations;

//comment: This class represents a news source in the application, including its properties and validation attributes.
namespace SC701.Models
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

        /// <summary>
        /// URLs adicionales del mismo sitio, separadas por salto de línea.
        /// Permite ingestar múltiples artículos de la misma fuente.
        /// </summary>
        [Display(Name = "URLs Adicionales")]
        public string? AdditionalUrls { get; set; }

        // Relación con SourceItems
        public virtual ICollection<SourceItem>? SourceItems { get; set; }

        // Helper: devuelve todas las URLs (principal + adicionales) como lista
        public List<string> GetAllUrls()
        {
            var urls = new List<string> { Url };

            if (!string.IsNullOrWhiteSpace(AdditionalUrls))
            {
                var extras = AdditionalUrls
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrWhiteSpace(u));
                urls.AddRange(extras);
            }

            return urls;
        }
    }
}