using System.ComponentModel.DataAnnotations;
using ITAnket.Data;

namespace ITAnket.Models
{
    public class AnketViewModel
    {
        [Display(Name = "İsim")]
        [Required(ErrorMessage = "İsim alanı zorunludur.")]
        public string Isim { get; set; } = "";

        [Display(Name = "Soyisim")]
        [Required(ErrorMessage = "Soyisim alanı zorunludur.")]
        public string Soyisim { get; set; } = "";

        [Display(Name = "E-Posta")]
        [Required(ErrorMessage = "E-posta alanı zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Mail { get; set; } = "";

        public List<Question> Questions { get; set; } = new();
        public Dictionary<int, int> Secimler { get; set; } = new();
    }
}
