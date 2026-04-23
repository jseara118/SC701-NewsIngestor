using System.ComponentModel.DataAnnotations;

namespace SC701.Models.ViewModels
{
    public class SettingsViewModel
    {
        [Required]
        [Display(Name = "Idioma")]
        public string Language { get; set; } = "es";

        [Required]
        [Display(Name = "Tema")]
        public string Theme { get; set; } = "dark";
    }
}
