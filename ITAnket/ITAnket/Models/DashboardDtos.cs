using System.ComponentModel.DataAnnotations;

namespace ITAnket.Models
{
    // Admin/Index ekranında kartlarda gösterilen soru-şık istatistikleri
    public class OptionStatDto
    {
        public int SecenekId { get; set; }
        public string Metin { get; set; } = "";
        public long Sayac { get; set; }
        public decimal Yuzde { get; set; }
    }

    public class QuestionStatDto
    {
        public int SoruId { get; set; }
        public string SoruMetni { get; set; } = "";
        public List<OptionStatDto> Secenekler { get; set; } = new();
    }

    // Detay satırı (cevap listesi tablosu)
    public class CevapSatiriDto
    {
        public DateTime Tarih { get; set; }
        public string Isim { get; set; } = "";
        public string Soyisim { get; set; } = "";
        public string Mail { get; set; } = "";
        public int SoruId { get; set; }
        public string Soru { get; set; } = "";
        public string Secenek { get; set; } = "";
    }

    // Admin/Index için birleşik ViewModel
    public class AdminDashboardViewModel
    {
        public int ToplamKatilimci { get; set; }
        public int Son7GunKatilim { get; set; }
        public List<QuestionStatDto> SoruIstatistikleri { get; set; } = new();
        public List<CevapSatiriDto> DetayCevaplar { get; set; } = new();
        public AdminFilterViewModel Filtre { get; set; } = new();
    }
}
