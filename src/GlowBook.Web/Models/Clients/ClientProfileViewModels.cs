using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Services;

namespace GlowBook.Web.Models.Clients;

public class ClientProfileEditViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [Display(Name = "Имя")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Телефон")]
    [Phone(ErrorMessage = "Некорректный телефон")]
    public string? Phone { get; set; }

    public bool HasAvatar { get; set; }

    public long? AvatarVersion { get; set; }
}

public class ClientChatViewModel
{
    public int ClientRecordId { get; set; }
    public string Title { get; set; } = "";
    public string? BackUrl { get; set; }
    public bool IsMasterView { get; set; }
    public string CurrentUserId { get; set; } = "";
    public List<ClientMessage> Messages { get; set; } = [];
}

public class ChatInboxViewModel
{
    public bool IsMasterView { get; set; }
    public IReadOnlyList<ChatConversationView> Conversations { get; set; } = [];
}
