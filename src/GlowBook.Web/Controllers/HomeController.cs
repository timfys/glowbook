using System.Diagnostics;
using GlowBook.Web.Configuration;
using GlowBook.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GlowBook.Web.Controllers;

public class HomeController : Controller
{
    private readonly GlowBookSettings _settings;

    public HomeController(IOptions<GlowBookSettings> settings)
    {
        _settings = settings.Value;
    }

    public IActionResult Index()
    {
        ViewBag.PremiumPrice = (int)_settings.PremiumPriceRub;
        return View();
    }

    /// <summary>Публичная страница услуг, оферты и реквизитов для ЮKassa.</summary>
    [HttpGet("/rekvizity")]
    [HttpGet("/legal")]
    public IActionResult Rekvizity()
    {
        ViewData["Title"] = "Реквизиты и оферта";
        ViewBag.PremiumPrice = (int)_settings.PremiumPriceRub;
        ViewBag.PremiumDays = _settings.PremiumDays;
        ViewBag.Legal = _settings.Legal;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
