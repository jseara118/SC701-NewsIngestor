using SC701.Models.DTOs;

namespace SC701.Models.DTOs
{
    /// <summary>
    /// ViewModel para mostrar items en la vista, ya sea desde BD o desde fuentes
    /// HU-21: Mostrar items desde fuentes si BD está vacía
    /// </summary>
    public class SourceItemViewModel
    {
        public int Id { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public string ComponentType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? NormalizedId { get; set; }
        public bool IsFromSource { get; set; }
        public StandardNewsItemDto? StandardItem { get; set; }
    }
}

