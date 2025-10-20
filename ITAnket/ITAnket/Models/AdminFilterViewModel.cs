using System.ComponentModel.DataAnnotations;

namespace ITAnket.Models
{
    /// <summary>Yönetim paneli filtre formu</summary>
    public class AdminFilterViewModel
    {
        [Display(Name = "Başlangıç")]
        [DataType(DataType.Date)]
        public DateTime? Baslangic { get; set; }

        [Display(Name = "Bitiş")]
        [DataType(DataType.Date)]
        public DateTime? Bitis { get; set; }

        [Display(Name = "İsim")] public string? Isim { get; set; }
        [Display(Name = "Soyisim")] public string? Soyisim { get; set; }
        [Display(Name = "E-posta")] public string? Mail { get; set; }

        [Display(Name = "Soru")] public int? SoruId { get; set; }
        [Display(Name = "Seçenek")] public int? SecenekId { get; set; }
    }
}

