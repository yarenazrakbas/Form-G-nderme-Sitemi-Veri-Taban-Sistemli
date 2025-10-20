using ITAnket.Data;
using ITAnket.Models;
using ITAnket.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITAnket.Controllers
{
    [AllowAnonymous]
    public class AnketController : Controller
    {
        private readonly AppDbContext _db;
        private readonly DuplicatePolicyOptions _dupOpt;

        public AnketController(AppDbContext db, IOptions<DuplicatePolicyOptions> dupOpt)
        {
            _db = db;
            _dupOpt = dupOpt.Value;
        }

        // ------------------------------------------------
        // TEŞEKKÜR
        // ------------------------------------------------
        [HttpGet("/Anket/Tesekkur")]
        public IActionResult Tesekkur() => View();

        // ------------------------------------------------
        // /Anket ve /Anket/Index → Aktif linke yönlendir (yoksa oluştur)
        // ------------------------------------------------
        [HttpGet("/Anket")]
        [HttpGet("/Anket/Index")]
        public async Task<IActionResult> Entry()
        {
            var kod = await _db.SurveyLinks.AsNoTracking()
                .Where(s => s.Aktif)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.Kod)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(kod))
            {
                var yeniKod = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

                _db.SurveyLinks.Add(new SurveyLink
                {
                    Kod = yeniKod,
                    Aktif = true,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                return Redirect($"/Anket/{yeniKod}");
            }

            return Redirect($"/Anket/{kod}");
        }

        // ------------------------------------------------
        // GET /Anket/{kod}  → Formu göster
        // ------------------------------------------------
        [HttpGet("/Anket/{kod}")]
        public async Task<IActionResult> Index(string kod)
        {
            var link = await _db.SurveyLinks.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Aktif && s.Kod == kod);

            if (link == null)
                return NotFound("Geçersiz anket linki.");

            var sorular = await _db.Questions
                .Include(q => q.Secenekler.OrderBy(s => s.SiraNo))
                .OrderBy(q => q.SiraNo)
                .ToListAsync();

            var vm = new SurveyFormViewModel
            {
                Kod = kod,
                Secimler = sorular.ToDictionary(q => q.Id, _ => 0)
            };

            ViewBag.Sorular = sorular;
            return View(vm);
        }

        // ------------------------------------------------
        // POST /Anket/{kod} → Kaydet
        // ------------------------------------------------
        [HttpPost("/Anket/{kod}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string kod, SurveyFormViewModel model)
        {
            var link = await _db.SurveyLinks.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Aktif && s.Kod == kod);

            if (link == null)
                return NotFound("Geçersiz anket linki.");

            var sorular = await _db.Questions
                .Include(q => q.Secenekler)
                .OrderBy(q => q.SiraNo)
                .ToListAsync();

            // ---- Form doğrulamaları
            model.Secimler ??= new Dictionary<int, int>();

            if (string.IsNullOrWhiteSpace(model.Isim))
                ModelState.AddModelError(nameof(model.Isim), "İsim zorunludur.");

            if (string.IsNullOrWhiteSpace(model.Soyisim))
                ModelState.AddModelError(nameof(model.Soyisim), "Soyisim zorunludur.");

            if (string.IsNullOrWhiteSpace(model.Mail))
                ModelState.AddModelError(nameof(model.Mail), "E-posta zorunludur.");

            foreach (var q in sorular)
            {
                if (!model.Secimler.TryGetValue(q.Id, out var optId) || optId == 0 ||
                    !q.Secenekler.Any(s => s.Id == optId))
                {
                    ModelState.AddModelError($"Secimler[{q.Id}]", "Lütfen bir seçenek seçiniz.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Sorular = sorular;
                return View(model);
            }

            // ---- Yinelenen doldurma politikası (e-posta + X gün)
            var engelTarih = DateTime.UtcNow.AddDays(-_dupOpt.BlockDays);
            var zatenVarMi = await _db.Respondents.AsNoTracking()
                .AnyAsync(r => r.Mail == model.Mail && r.CreatedAt >= engelTarih);

            if (zatenVarMi)
            {
                ModelState.AddModelError(nameof(model.Mail),
                    $"Bu e-posta ile son {_dupOpt.BlockDays} gün içinde anket doldurulmuş görünüyor.");
                ViewBag.Sorular = sorular;
                return View(model);
            }

            // ---- Kayıt + İstatistik (UPSERT) → Transaction
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                // 1) Respondent
                var respondent = new Respondent
                {
                    Isim = model.Isim!.Trim(),
                    Soyisim = model.Soyisim!.Trim(),
                    Mail = model.Mail!.Trim().ToLowerInvariant(),
                    CreatedAt = DateTime.UtcNow
                };
                _db.Respondents.Add(respondent);
                await _db.SaveChangesAsync();

                // 2) Responses
                var now = DateTime.UtcNow;
                foreach (var (questionId, answerOptionId) in model.Secimler)
                {
                    _db.Responses.Add(new Response
                    {
                        RespondentId = respondent.Id,
                        QuestionId = questionId,
                        AnswerOptionId = answerOptionId,
                        CreatedAt = now
                    });
                }
                await _db.SaveChangesAsync();

                // 3) Stats UPSERT (varsa +1, yoksa oluştur)
                foreach (var (qid, aoid) in model.Secimler)
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync($@"
                        INSERT INTO Stats (QuestionId, AnswerOptionId, Sayac, Yuzde)
                        VALUES ({qid}, {aoid}, 1, 0)
                        ON DUPLICATE KEY UPDATE Sayac = Sayac + 1;");
                }

                // 4) Yüzdeleri toplu güncelle (INT bölmesi yok)
                await _db.Database.ExecuteSqlRawAsync(@"
                    UPDATE Stats s
                    JOIN (
                        SELECT QuestionId, SUM(Sayac) AS Toplam
                        FROM Stats
                        GROUP BY QuestionId
                    ) t ON t.QuestionId = s.QuestionId
                    SET s.Yuzde = ROUND(
                        (CAST(s.Sayac AS DECIMAL(18,4)) * 100.0) / NULLIF(CAST(t.Toplam AS DECIMAL(18,4)), 0),
                        2
                    );");

                await tx.CommitAsync();
            });

            return RedirectToAction(nameof(Tesekkur));
        }
    }
}
