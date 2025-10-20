using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITAnket.Data
{
    // Yönetici
    public class AdminUser
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required] public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        [Required] public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
        [Required] public int IterationCount { get; set; }

        [Required] public DateTime CreatedAt { get; set; }
    }

    // Evrensel anket linki
    public class SurveyLink
    {
        public int Id { get; set; }
        [Required, MaxLength(64)]
        public string Kod { get; set; } = string.Empty;
        [Required]
        public bool Aktif { get; set; } = true;
        [Required]
        public DateTime CreatedAt { get; set; }
    }

    // Soru
    public class Question
    {
        [Key] public int Id { get; set; }
        [Required] public int SiraNo { get; set; }
        [Required, MaxLength(500)] public string Metin { get; set; } = string.Empty;

        public ICollection<AnswerOption> Secenekler { get; set; } = new List<AnswerOption>();
    }

    // Şık
    public class AnswerOption
    {
        [Key] public int Id { get; set; }
        [Required] public int QuestionId { get; set; }
        [Required] public int SiraNo { get; set; }
        [Required, MaxLength(200)] public string Metin { get; set; } = string.Empty;

        public Question? Question { get; set; }
    }

    // Katılımcı
    public class Respondent
    {
        public long Id { get; set; }
        [Required, MaxLength(100)] public string Isim { get; set; } = string.Empty;
        [Required, MaxLength(100)] public string Soyisim { get; set; } = string.Empty;

        [Required, MaxLength(200), EmailAddress]
        public string Mail { get; set; } = string.Empty;

        [Required] public DateTime CreatedAt { get; set; }

        public ICollection<Response> Yanits { get; set; } = new List<Response>();
    }

    // Tek bir soru için tek bir cevap
    public class Response
    {
        public long Id { get; set; }
        [Required] public long RespondentId { get; set; }
        [Required] public int QuestionId { get; set; }
        [Required] public int AnswerOptionId { get; set; }
        [Required] public DateTime CreatedAt { get; set; }

        public Respondent? Respondent { get; set; }
        public Question? Question { get; set; }
        public AnswerOption? AnswerOption { get; set; }
    }

    // İstatistik: her soru-şık için sayaç ve yüzde
    public class Stat
    {
        public int Id { get; set; }
        [Required] public int QuestionId { get; set; }
        [Required] public int AnswerOptionId { get; set; }
        [Required] public long Sayac { get; set; }
        [Required, Column(TypeName = "decimal(5,2)")] public decimal Yuzde { get; set; }

        public Question? Question { get; set; }
        public AnswerOption? AnswerOption { get; set; }
    }
}
