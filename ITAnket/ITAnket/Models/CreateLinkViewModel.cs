namespace ITAnket.Models
{
    /// <summary>Ana sayfadaki “Evrensel Anket Linki” kutusu için ViewModel</summary>
    public class CreateLinkViewModel
    {
        /// <summary>Örn: E411AA98</summary>
        public string? MevcutKod { get; set; }

        /// <summary>Örn: https://localhost:7220/Anket/E411AA98</summary>
        public string? TamLink { get; set; }

        /// <summary>Bilgilendirme mesajı için: bu açılışta yeni kod üretildi mi?</summary>
        public bool YeniUretildi { get; set; }

        /// <summary>Geriye dönük uyumluluk / eski view’ler: Mevcut link gösterimi</summary>
        public string? MevcutLink
        {
            get => TamLink;
            set => TamLink = value;
        }
    }
}
