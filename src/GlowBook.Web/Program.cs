using GlowBook.Web.Configuration;
using GlowBook.Web.Data;
using GlowBook.Web.Extensions;
using GlowBook.Web.Models;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

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
