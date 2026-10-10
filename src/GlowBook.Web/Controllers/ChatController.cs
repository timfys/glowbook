using GlowBook.Web.Data;
using GlowBook.Web.Models;
using GlowBook.Web.Models.Clients;
using GlowBook.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlowBook.Web.Controllers;

[Authorize]
[Route("chat")]
public class ChatController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly ClientChatService _chat;

    public ChatController(
        UserManager<ApplicationUser> users,
        ApplicationDbContext db,
        ClientChatService chat)
    {
        _users = users;
        _db = db;
        _chat = chat;
    }

    [HttpGet("thread/{clientRecordId:int}")]
    public async Task<IActionResult> Thread(int clientRecordId)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!await _chat.CanAccessChatAsync(clientRecordId, user.Id))
            return Forbid();

        var client = await _db.Clients
            .Include(c => c.MasterProfile)
            .FirstOrDefaultAsync(c => c.Id == clientRecordId && !c.IsArchived);
        if (client?.MasterProfile == null)
            return NotFound();

        var master = client.MasterProfile;
        var isMasterView = master.UserId == user.Id;
        var title = isMasterView
            ? client.Name
            : (string.IsNullOrWhiteSpace(master.BusinessName) ? "Мастер" : master.BusinessName);

        var messages = await _chat.GetMessagesAsync(clientRecordId);
        var backUrl = isMasterView
            ? Url.Action("Chats", "Clients")
            : (ClientAccountService.IsClient(user)
                ? Url.Action("Chats", "My")
                : Url.Action("Profile", "Blog", new { username = master.BookingSlug }));

        return View("~/Views/My/Chat.cshtml", new ClientChatViewModel
        {
            ClientRecordId = clientRecordId,
            Title = title,
            BackUrl = backUrl,
            IsMasterView = isMasterView,
            CurrentUserId = user.Id,
            Messages = messages
        });
    }
}
