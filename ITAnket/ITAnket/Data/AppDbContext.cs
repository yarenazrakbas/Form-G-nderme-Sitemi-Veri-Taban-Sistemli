using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

namespace ITAnket.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
        public DbSet<SurveyLink> SurveyLinks => Set<SurveyLink>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
        public DbSet<Respondent> Respondents => Set<Respondent>();
        public DbSet<Response> Responses => Set<Response>();
        public DbSet<Stat> Stats => Set<Stat>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            mb.Entity<AnswerOption>()
              .HasIndex(a => new { a.QuestionId, a.SiraNo }).IsUnique();

            mb.Entity<Stat>()
              .HasIndex(s => new { s.QuestionId, s.AnswerOptionId }).IsUnique();

            // 10 soru seed
            var qs = new (int id, int sira, string metin)[]
            {
                (1,1,"1- Bilgi İşlem ekibine ilettiğiniz sorunların çözüm süresinden ne kadar memnunsunuz?"),
                (2,2,"2- Çözülen sorunların kalıcı ve tatmin edici olduğunu düşünüyor musunuz?"),
                (3,3,"3- Bilgi işlem ekibi sizinle iletişim kurarken yeterince açık, anlaşılır ve profesyonel mi?"),
                (4,4,"4- Sorun yaşadığınızda Bilgi işlem ekibine ulaşmak ne kadar kolay oluyor?"),
                (5,5,"5- Departman, acil konulara gerekli önceliği veriyor mu?"),
                (6,6,"6- Bilgi işlem ekibi olası sorunları öngörüp önceden önlem alıyor mu?"),
                (7,7,"7- Şirketin sunduğu bilgisayar, ağ, yazılım vb. teknolojik altyapının yeterliliğini nasıl değerlendirirsiniz?"),
                (8,8,"8- Bilgi işlem departmanı, yeni sistemler/yazılımlar için yeterli eğitim veya bilgilendirme sağlıyor mu?"),
                (9,9,"9- Bilgi İşlem ekibi sizinle işbirliği yaparken çözüm odaklı bir yaklaşım sergiliyor mu?"),
                (10,10,"10- Genel olarak Bilgi İşlem departmanının performansından ne kadar memnunsunuz?")
            };
            mb.Entity<Question>().HasData(qs.Select(q => new Question { Id = q.id, SiraNo = q.sira, Metin = q.metin }));

            // Şıklar (soru özelinde)
            // 1: Çok memnunum – Memnunum – Kararsızım – Memnun değilim – Hiç memnun değilim
            var opts = new List<AnswerOption>();
            void add5(int qId, params string[] op)
            {
                for (int i = 0; i < op.Length; i++)
                {
                    // deterministik Id: qId*10 + (i+1)
                    opts.Add(new AnswerOption { Id = qId * 10 + (i + 1), QuestionId = qId, SiraNo = i + 1, Metin = op[i] });
                }
            }

            add5(1, "Çok memnunum", "Memnunum", "Kararsızım", "Memnun değilim", "Hiç memnun değilim");
            add5(2, "Kesinlikle katılıyorum", "Katılıyorum", "Kararsızım", "Katılmıyorum", "Kesinlikle katılmıyorum");
            add5(3, "Kesinlikle katılıyorum", "Katılıyorum", "Kararsızım", "Katılmıyorum", "Kesinlikle katılmıyorum");
            add5(4, "Çok kolay", "Kolay", "Ne kolay ne zor", "Zor", "Çok zor");
            add5(5, "Her zaman", "Çoğu zaman", "Bazen", "Nadiren", "Hiçbir zaman");
            add5(6, "Kesinlikle katılıyorum", "Katılıyorum", "Kararsızım", "Katılmıyorum", "Kesinlikle katılmıyorum");
            add5(7, "Çok yeterli", "Yeterli", "Orta düzeyde", "Yetersiz", "Çok yetersiz");
            add5(8, "Her zaman", "Çoğu zaman", "Bazen", "Nadiren", "Hiçbir zaman");
            add5(9, "Kesinlikle katılıyorum", "Katılıyorum", "Kararsızım", "Katılmıyorum", "Kesinlikle katılmıyorum");
            add5(10, "Çok memnunum", "Memnunum", "Kararsızım", "Memnun değilim", "Hiç memnun değilim");

            mb.Entity<AnswerOption>().HasData(opts);

            // Stats seed (0 sayaç, 0%)
            int sid = 1;
            mb.Entity<Stat>().HasData(
                opts.Select(o => new Stat
                {
                    Id = sid++,
                    QuestionId = o.QuestionId,
                    AnswerOptionId = o.Id,
                    Sayac = 0,
                    Yuzde = 0
                })
            );
        }
    }
}
