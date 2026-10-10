using GlowBook.Web.Models.Blog;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlowBook.Web.Controllers;

[AllowAnonymous]
[Route("search")]
public class SearchController : Controller
{
    private readonly MasterSearchService _search;

    public SearchController(MasterSearchService search) => _search = search;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var results = string.IsNullOrWhiteSpace(q)
            ? []
            : await _search.SearchAsync(q, 40, ct);

        return View(new MasterSearchViewModel { Query = q, Results = results });
    }
}
