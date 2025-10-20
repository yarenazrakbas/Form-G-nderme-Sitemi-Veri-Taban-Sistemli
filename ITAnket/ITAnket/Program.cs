using System.Globalization;
using ITAnket.Data;
using ITAnket.Models;                 // SurveyLink i�in
using ITAnket.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// ------------------------------
// Yerelle�tirme (TR)
// ------------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ------------------------------
// EF Core + MySQL
// ------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("MySql");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException(
            "MySql ba�lant� dizesi bulunamad�. appsettings(.Development).json i�indeki ConnectionStrings:MySql de�erini kontrol edin.");

    var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
    options.UseMySql(cs, serverVersion, opt => opt.EnableRetryOnFailure());
});

// ------------------------------
// Kimlik Do�rulama (Cookie) + CSRF
// ------------------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Yonetim/Giris";
        options.AccessDeniedPath = "/Yonetim/Giris";
        options.Cookie.HttpOnly = true;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAntiforgery();

// Opsiyonlar + DI
builder.Services.Configure<AdminSeedOptions>(builder.Configuration.GetSection("AdminInitial"));
builder.Services.Configure<DuplicatePolicyOptions>(builder.Configuration.GetSection("DuplicatePolicy"));
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();



var app = builder.Build();

// ------------------------------
// �stek Yerelle�tirme (tr-TR)
// ------------------------------
var supportedCultures = new[] { new CultureInfo("tr-TR") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("tr-TR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// ------------------------------
// Orta Katmanlar
// ------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// ------------------------------
// K�K URL ? Aktif Anket (yoksa olu�tur)
// ------------------------------
app.MapGet("/", async context =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var kod = await db.SurveyLinks
        .Where(s => s.Aktif)
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => s.Kod)
        .FirstOrDefaultAsync();

    if (string.IsNullOrWhiteSpace(kod))
    {
        var yeniKod = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        db.SurveyLinks.Add(new SurveyLink
        {
            Kod = yeniKod,
            Aktif = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        context.Response.Redirect($"/Anket/{yeniKod}");
        return;
    }

    context.Response.Redirect($"/Anket/{kod}");
});

// ------------------------------
// Varsay�lan Rota: Anket/Index
// ------------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Anket}/{action=Index}/{id?}");

// ------------------------------
// Veritaban� Migration + Seed
// ------------------------------
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider, db);
}
catch (Exception ex)
{
    Console.WriteLine(">>> Uygulama ba�lang�c�nda hata:");
    Console.WriteLine(ex.ToString());
    throw;
}

app.Run();
