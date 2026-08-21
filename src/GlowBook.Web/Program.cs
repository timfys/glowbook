using System.Globalization;
using GlowBook.Web.Configuration;
using GlowBook.Web.Data;
using GlowBook.Web.Extensions;
using GlowBook.Web.Models;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var ru = new CultureInfo("ru-RU");
CultureInfo.DefaultThreadCurrentCulture = ru;
CultureInfo.DefaultThreadCurrentUICulture = ru;

var builder = WebApplication.CreateBuilder(args);

var dataDir = ResolveDataDirectory(builder);
Directory.CreateDirectory(dataDir);

var connectionString = ResolveSqliteConnection(builder.Configuration, dataDir);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 3 * 1024 * 1024;
});

builder.Services.Configure<GlowBookSettings>(builder.Configuration.GetSection(GlowBookSettings.SectionName));
builder.Services.Configure<YooKassaSettings>(builder.Configuration.GetSection(YooKassaSettings.SectionName));

builder.Services.AddGlowBookAuthentication(builder.Configuration);

builder.Services.AddScoped<MasterProfileService>();
builder.Services.AddScoped<ExternalAccountService>();
builder.Services.AddScoped<TelegramAuthService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddHttpClient<YooKassaService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();
app.Logger.LogInformation("SQLite: {ConnectionString}", connectionString);

await DbInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

static string ResolveDataDirectory(WebApplicationBuilder builder)
{
    var configured = builder.Configuration["DATA_DIR"];
    if (!string.IsNullOrWhiteSpace(configured))
        return configured;

    var railwayVolume = Environment.GetEnvironmentVariable("RAILWAY_VOLUME_MOUNT_PATH");
    if (!string.IsNullOrWhiteSpace(railwayVolume))
        return railwayVolume;

    if (Directory.Exists("/data"))
        return "/data";

    return Path.Combine(builder.Environment.ContentRootPath, "Data");
}

static string ResolveSqliteConnection(IConfiguration configuration, string dataDir)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    var dbPath = Path.Combine(dataDir, "glowbook.db");

    if (string.IsNullOrWhiteSpace(connectionString))
        return $"Data Source={dbPath}";

    if (connectionString.Contains("Data/glowbook.db", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains(@"Data\glowbook.db", StringComparison.OrdinalIgnoreCase))
        return $"Data Source={dbPath}";

    return connectionString;
}
