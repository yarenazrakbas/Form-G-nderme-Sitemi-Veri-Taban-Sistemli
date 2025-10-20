using System.ComponentModel.DataAnnotations;

namespace ITAnket.Models
{
    public class AdminLoginViewModel
    {
        [Display(Name = "Kullanıcı Adı")]
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        public string KullaniciAdi { get; set; } = "";

        [Display(Name = "Parola")]
        [Required(ErrorMessage = "Parola zorunludur.")]
        [DataType(DataType.Password)]
        public string Parola { get; set; } = "";
    }
}
