using ITAnket.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITAnket.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider sp, AppDbContext db)
        {
            // Admin kullanıcı yoksa oluştur
            if (!await db.AdminUsers.AnyAsync())
            {
                var opt = sp.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
                var hasher = sp.GetRequiredService<IPasswordHasher>();
                var (hash, salt) = hasher.Hash(opt.Password, opt.Iterations);

                db.AdminUsers.Add(new AdminUser
                {
                    KullaniciAdi = opt.Username,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IterationCount = opt.Iterations,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            // Evrensel link yoksa 1 tane üret
            if (!await db.SurveyLinks.AnyAsync())
            {
                db.SurveyLinks.Add(new SurveyLink
                {
                    Kod = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    Aktif = true,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
