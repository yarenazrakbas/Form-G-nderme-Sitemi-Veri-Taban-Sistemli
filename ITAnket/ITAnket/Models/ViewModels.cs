// Models/SurveyFormViewModel.cs
using System.ComponentModel.DataAnnotations;
using ITAnket.Data;

namespace ITAnket.Models
{
    public class SurveyFormViewModel : IValidatableObject
    {
        [Required, Display(Name = "Anket Kodu")]
        public string Kod { get; set; } = "";

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

        public List<Question> Sorular { get; set; } = new();

        // SoruId -> SeçenekId
        public Dictionary<int, int> Secimler { get; set; } = new();

        // Sunucu tarafı: Her soru için bir şık işaretlendi mi?
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var soru in Sorular)
            {
                if (!Secimler.TryGetValue(soru.Id, out _))
                {
                    yield return new ValidationResult(
                        "Lütfen bir seçenek seçiniz.",
                        new[] { $"Secimler[{soru.Id}]" }
                    );
                }
            }
        }
    }
}
