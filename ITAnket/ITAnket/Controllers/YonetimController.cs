using System.Security.Claims;
using ClosedXML.Excel;
using ITAnket.Data;
using ITAnket.Models;
using ITAnket.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITAnket.Controllers
{
    public class YonetimController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        public YonetimController(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        [HttpGet]
        public IActionResult Giris() => View(new AdminLoginViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Giris(AdminLoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var admin = await _db.AdminUsers.AsNoTracking()
                .FirstOrDefaultAsync(a => a.KullaniciAdi == vm.KullaniciAdi);

            if (admin == null || !_hasher.Verify(vm.Parola, admin.PasswordHash, admin.PasswordSalt, admin.IterationCount))
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı adı veya parola hatalı.");
                return View(vm);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, admin.KullaniciAdi),
                new Claim("role", "admin")
            };

            var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(id));

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Cikis()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Giris));
        }

        // =======================
        // YÖNETİM PANELİ
        // =======================
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] AdminFilterViewModel f)
        {
            var now = DateTime.UtcNow;
            var toplam = await _db.Respondents.CountAsync();
            var son7 = await _db.Respondents.CountAsync(r => r.CreatedAt >= now.AddDays(-7));

            var soruList = await _db.Questions.AsNoTracking()
                .OrderBy(q => q.SiraNo)
                .Select(q => new { q.Id, q.Metin })
                .ToListAsync();

            var soruIds = soruList.Select(x => x.Id).ToList();

            var secenekList = await _db.AnswerOptions.AsNoTracking()
                .Where(s => soruIds.Contains(s.QuestionId))
                .OrderBy(s => s.QuestionId).ThenBy(s => s.SiraNo)
                .Select(s => new { s.Id, s.QuestionId, s.Metin })
                .ToListAsync();

            // COUNT(*) -> long gelebilir
            var counts = await _db.Responses.AsNoTracking()
                .Where(r => soruIds.Contains(r.QuestionId))
                .GroupBy(r => new { r.QuestionId, r.AnswerOptionId })
                .Select(g => new { g.Key.QuestionId, g.Key.AnswerOptionId, Sayac = (long)g.Count() })
                .ToListAsync();

            var countDict = counts.ToDictionary(k => (k.QuestionId, k.AnswerOptionId), v => v.Sayac);

            // --- Soru bazlı yüzdeler (mevcut grafikler için) ---
            var stats = new List<QuestionStatDto>();
            foreach (var q in soruList)
            {
                var opts = secenekList.Where(s => s.QuestionId == q.Id).ToList();
                long toplamSayac = opts.Sum(o => countDict.TryGetValue((q.Id, o.Id), out var c) ? c : 0L);

                var optStats = new List<OptionStatDto>();
                foreach (var o in opts)
                {
                    long sayacL = countDict.TryGetValue((q.Id, o.Id), out var c) ? c : 0L;
                    decimal yuzde = toplamSayac > 0
                        ? Math.Round((decimal)sayacL * 100m / toplamSayac, 2)
                        : 0m;

                    optStats.Add(new OptionStatDto
                    {
                        SecenekId = o.Id,
                        Metin = o.Metin,
                        Sayac = (int)Math.Min(sayacL, int.MaxValue), // güvenli cast
                        Yuzde = yuzde
                    });
                }

                stats.Add(new QuestionStatDto
                {
                    SoruId = q.Id,
                    SoruMetni = q.Metin,
                    Secenekler = optStats
                });
            }

            // ---------------------------
            // 1) GENEL PASTA: TÜM CEVAPLARIN YÜZDE DAĞILIMI (toplamdan)
            // ---------------------------
            string[] overallLabels =
            {
                "Çok memnunum",
                "Memnunum",
                "Kararsızım",
                "Memnun değilim",
                "Hiç memnun değilim"
            };

            string MapToCanon(string s)
            {
                s = (s ?? string.Empty).Trim();

                if (s.Equals("Çok memnunum", StringComparison.OrdinalIgnoreCase)) return "Çok memnunum";
                if (s.Equals("Memnunum", StringComparison.OrdinalIgnoreCase)) return "Memnunum";
                if (s.Equals("Kararsızım", StringComparison.OrdinalIgnoreCase)) return "Kararsızım";
                if (s.Equals("Memnun değilim", StringComparison.OrdinalIgnoreCase)) return "Memnun değilim";
                if (s.Equals("Hiç memnun değilim", StringComparison.OrdinalIgnoreCase)) return "Hiç memnun değilim";

                // 5'li likert eşlemeleri
                if (s.Equals("Kesinlikle katılıyorum", StringComparison.OrdinalIgnoreCase)) return "Çok memnunum";
                if (s.Equals("Katılıyorum", StringComparison.OrdinalIgnoreCase)) return "Memnunum";
                if (s.Equals("Katılmıyorum", StringComparison.OrdinalIgnoreCase)) return "Memnun değilim";
                if (s.Equals("Kesinlikle katılmıyorum", StringComparison.OrdinalIgnoreCase)) return "Hiç memnun değilim";

                // Bilinmeyen her şey nötr
                return "Kararsızım";
            }

            // Tüm yanıtları çek (kategori ve ortalama için)
            var tumCevaplar = await _db.Responses
                .AsNoTracking()
                .Include(r => r.AnswerOption)
                .Include(r => r.Question)
                .Where(r => r.AnswerOptionId != 0 && r.QuestionId != 0)
                .ToListAsync();

            var overallCountDict = overallLabels.ToDictionary(x => x, _ => 0L);

            foreach (var r in tumCevaplar)
            {
                var canon = MapToCanon(r.AnswerOption?.Metin ?? "");
                overallCountDict[canon]++;
            }

            long totalResponses = overallCountDict.Values.Sum();
            var overallCounts = overallLabels.Select(k => (int)Math.Min(overallCountDict[k], int.MaxValue)).ToArray();
            var overallPerc = overallLabels
                .Select(k => totalResponses > 0 ? Math.Round((decimal)overallCountDict[k] * 100m / totalResponses, 2) : 0m)
                .ToArray();

            ViewBag.OverallLabels = overallLabels; // pasta grafiği etiketleri
            ViewBag.OverallCounts = overallCounts; // adet
            ViewBag.OverallPerc = overallPerc;     // yüzde (%)

            // ---------------------------
            // 2) SORU BAZLI ORTALAMA (1..5) – tek grafikte her soru için bar
            // ---------------------------
            int MapToScore(string metin)
            {
                metin = (metin ?? "").Trim();

                if (metin.Equals("Çok memnunum", StringComparison.OrdinalIgnoreCase)) return 5;
                if (metin.Equals("Memnunum", StringComparison.OrdinalIgnoreCase)) return 4;
                if (metin.Equals("Kararsızım", StringComparison.OrdinalIgnoreCase)) return 3;
                if (metin.Equals("Memnun değilim", StringComparison.OrdinalIgnoreCase)) return 2;
                if (metin.Equals("Hiç memnun değilim", StringComparison.OrdinalIgnoreCase)) return 1;

                // Likert eşlemesi
                if (metin.Equals("Kesinlikle katılıyorum", StringComparison.OrdinalIgnoreCase)) return 5;
                if (metin.Equals("Katılıyorum", StringComparison.OrdinalIgnoreCase)) return 4;
                if (metin.Equals("Katılmıyorum", StringComparison.OrdinalIgnoreCase)) return 2;
                if (metin.Equals("Kesinlikle katılmıyorum", StringComparison.OrdinalIgnoreCase)) return 1;

                return 3; // bilinmeyene nötr
            }

            var soruTextById = soruList.ToDictionary(x => x.Id, x => x.Metin);
            var soruOrtalamalari = new List<(string Soru, decimal Ortalama)>();

            foreach (var g in tumCevaplar.GroupBy(r => r.QuestionId))
            {
                var skorlar = g.Select(r => MapToScore(r.AnswerOption?.Metin ?? ""));
                var avg = skorlar.Any() ? Math.Round((decimal)skorlar.Average(), 2) : 0m;

                if (soruTextById.TryGetValue(g.Key, out var soruText))
                    soruOrtalamalari.Add((soruText, avg));
            }

            // Görünüm için: etiketler ve değerler (sıra no'ya göre)
            var avgLabels = soruList
                .Where(q => soruOrtalamalari.Any(x => x.Soru == q.Metin))
                .Select(q => q.Metin)
                .ToArray();

            var avgValues = avgLabels
                .Select(lbl => soruOrtalamalari.First(x => x.Soru == lbl).Ortalama)
                .ToArray();

            ViewBag.AvgLabels = avgLabels;    // her soru metni
            ViewBag.AvgValues = avgValues;    // 1..5 ortalama

            // ---- Detaylı kayıtlar (filtreli) ----
            var query =
                from resp in _db.Responses.AsNoTracking()
                join per in _db.Respondents.AsNoTracking() on resp.RespondentId equals per.Id
                join soru in _db.Questions.AsNoTracking() on resp.QuestionId equals soru.Id
                join sec in _db.AnswerOptions.AsNoTracking() on resp.AnswerOptionId equals sec.Id
                select new CevapSatiriDto
                {
                    Tarih = resp.CreatedAt,
                    Isim = per.Isim,
                    Soyisim = per.Soyisim,
                    Mail = per.Mail,
                    SoruId = resp.QuestionId,
                    Soru = soru.Metin,
                    Secenek = sec.Metin
                };

            if (f.Baslangic.HasValue) query = query.Where(x => x.Tarih >= f.Baslangic.Value.ToUniversalTime());
            if (f.Bitis.HasValue) query = query.Where(x => x.Tarih < f.Bitis.Value.ToUniversalTime().AddDays(1));
            if (!string.IsNullOrWhiteSpace(f.Isim)) query = query.Where(x => x.Isim.Contains(f.Isim));
            if (!string.IsNullOrWhiteSpace(f.Soyisim)) query = query.Where(x => x.Soyisim.Contains(f.Soyisim));
            if (!string.IsNullOrWhiteSpace(f.Mail)) query = query.Where(x => x.Mail.ToLower().Contains(f.Mail.ToLower()));
            if (f.SoruId.HasValue) query = query.Where(x => x.SoruId == f.SoruId.Value);
            if (f.SecenekId.HasValue)
            {
                var metin = await _db.AnswerOptions.AsNoTracking()
                    .Where(a => a.Id == f.SecenekId.Value)
                    .Select(a => a.Metin)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(metin))
                    query = query.Where(x => x.Secenek == metin);
            }

            var detay = await query.OrderByDescending(x => x.Tarih).Take(1000).ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                ToplamKatilimci = toplam,
                Son7GunKatilim = son7,
                SoruIstatistikleri = stats,
                DetayCevaplar = detay,
                Filtre = f
            };

            return View(vm);
        }

        // =======================
        // TÜM VERİLERİ TEMİZLE
        // =======================
        [Authorize]
        [HttpPost("/Yonetim/Temizle")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Temizle()
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                await _db.Database.ExecuteSqlRawAsync("DELETE FROM Responses;");
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM Respondents;");
                await _db.Database.ExecuteSqlRawAsync("UPDATE Stats SET Sayac = 0, Yuzde = 0;");

                await tx.CommitAsync();
            });

            TempData["Mesaj"] = "Tüm anket verileri silindi ve istatistikler sıfırlandı.";
            return RedirectToAction(nameof(Index));
        }

        // =======================
        // EXCEL DIŞA AKTAR
        // =======================
        [Authorize]
        [HttpGet("/Yonetim/Excel")]
        public async Task<IActionResult> Excel([FromQuery] AdminFilterViewModel f)
        {
            var query =
                from resp in _db.Responses.AsNoTracking()
                join per in _db.Respondents.AsNoTracking() on resp.RespondentId equals per.Id
                join soru in _db.Questions.AsNoTracking() on resp.QuestionId equals soru.Id
                join sec in _db.AnswerOptions.AsNoTracking() on resp.AnswerOptionId equals sec.Id
                select new
                {
                    resp.CreatedAt,
                    per.Isim,
                    per.Soyisim,
                    per.Mail,
                    Soru = soru.Metin,
                    Secenek = sec.Metin
                };

            if (f.Baslangic.HasValue) query = query.Where(x => x.CreatedAt >= f.Baslangic.Value.ToUniversalTime());
            if (f.Bitis.HasValue) query = query.Where(x => x.CreatedAt < f.Bitis.Value.ToUniversalTime().AddDays(1));
            if (!string.IsNullOrWhiteSpace(f.Isim)) query = query.Where(x => x.Isim.Contains(f.Isim));
            if (!string.IsNullOrWhiteSpace(f.Soyisim)) query = query.Where(x => x.Soyisim.Contains(f.Soyisim));
            if (!string.IsNullOrWhiteSpace(f.Mail)) query = query.Where(x => x.Mail.ToLower().Contains(f.Mail.ToLower()));

            var rows = await query.OrderBy(x => x.CreatedAt).ToListAsync();

            using var wb = new XLWorkbook();

            var ws = wb.Worksheets.Add("Cevaplar");
            ws.Cell(1, 1).Value = "Tarih (UTC)";
            ws.Cell(1, 2).Value = "İsim";
            ws.Cell(1, 3).Value = "Soyisim";
            ws.Cell(1, 4).Value = "E-Posta";
            ws.Cell(1, 5).Value = "Soru";
            ws.Cell(1, 6).Value = "Seçenek";

            int rowIndex = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowIndex, 1).Value = row.CreatedAt;
                ws.Cell(rowIndex, 2).Value = row.Isim;
                ws.Cell(rowIndex, 3).Value = row.Soyisim;
                ws.Cell(rowIndex, 4).Value = row.Mail;
                ws.Cell(rowIndex, 5).Value = row.Soru;
                ws.Cell(rowIndex, 6).Value = row.Secenek;
                rowIndex++;
            }
            ws.Columns().AdjustToContents();

            var ws2 = wb.Worksheets.Add("Istatistik");
            ws2.Cell(1, 1).Value = "Soru";
            ws2.Cell(1, 2).Value = "Seçenek";
            ws2.Cell(1, 3).Value = "Sayaç";
            ws2.Cell(1, 4).Value = "Yüzde";

            var soruList = await _db.Questions.AsNoTracking().OrderBy(q => q.SiraNo).ToListAsync();
            var secenekList = await _db.AnswerOptions.AsNoTracking()
                .Where(a => soruList.Select(x => x.Id).Contains(a.QuestionId))
                .OrderBy(a => a.QuestionId).ThenBy(a => a.SiraNo)
                .ToListAsync();

            var counts = await _db.Responses.AsNoTracking()
                .GroupBy(r => new { r.QuestionId, r.AnswerOptionId })
                .Select(g => new { g.Key.QuestionId, g.Key.AnswerOptionId, Sayac = (long)g.Count() })
                .ToListAsync();

            var dict = counts.ToDictionary(k => (k.QuestionId, k.AnswerOptionId), v => v.Sayac);

            int rowIndex2 = 2;
            foreach (var q in soruList)
            {
                var opts = secenekList.Where(s => s.QuestionId == q.Id).ToList();
                long toplamSayac = opts.Sum(o => dict.TryGetValue((q.Id, o.Id), out var c) ? c : 0L);

                foreach (var o in opts)
                {
                    long sayacL = dict.TryGetValue((q.Id, o.Id), out var c) ? c : 0L;
                    decimal yuzde = toplamSayac > 0 ? Math.Round((decimal)sayacL * 100m / toplamSayac, 2) : 0m;

                    ws2.Cell(rowIndex2, 1).Value = q.Metin;
                    ws2.Cell(rowIndex2, 2).Value = o.Metin;
                    ws2.Cell(rowIndex2, 3).Value = (int)Math.Min(sayacL, int.MaxValue);
                    ws2.Cell(rowIndex2, 4).Value = yuzde;
                    rowIndex2++;
                }
            }
            ws2.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;

            var fileName = $"IT_Anket_Rapor_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
