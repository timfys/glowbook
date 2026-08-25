using System.ComponentModel.DataAnnotations;
using GlowBook.Web.Models.Entities;
using GlowBook.Web.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace GlowBook.Web.Models.Clients;

public class ClientDetailsViewModel
{
    public Client Client { get; set; } = null!;
    public IReadOnlyList<Appointment> Appointments { get; set; } = [];
    public IReadOnlyList<TreatmentRecord> Treatments { get; set; } = [];
    public IReadOnlyList<ClientPhoto> Photos { get; set; } = [];
    public IReadOnlyList<HomeCarePrescription> HomeCare { get; set; } = [];
}

public class TreatmentFormModel
{
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Укажите дату процедуры")]
    [Display(Name = "Дата")]
    public DateTime PerformedAt { get; set; } = DateTime.Today.AddHours(12);

    [Display(Name = "Услуга")]
    public int? ServiceId { get; set; }

    [Display(Name = "Название процедуры")]
    [StringLength(200)]
    public string? ProcedureName { get; set; }

    [Display(Name = "Препараты")]
    [StringLength(1000)]
    public string? ProductsUsed { get; set; }

    [Display(Name = "Аппарат")]
    [StringLength(500)]
    public string? EquipmentUsed { get; set; }

    [Display(Name = "Заметки")]
    public string? Notes { get; set; }

    [Display(Name = "Стоимость")]
    [Range(0, 1000000)]
    public decimal? Price { get; set; }
}

public class PhotoUploadModel
{
    public int ClientId { get; set; }

    [Required]
    [Display(Name = "Тип")]
    public PhotoKind Kind { get; set; } = PhotoKind.Before;

    [Required(ErrorMessage = "Выберите фото")]
    [Display(Name = "Фото")]
    public IFormFile? File { get; set; }

    [Display(Name = "Подпись")]
    [StringLength(300)]
    public string? Caption { get; set; }

    [Display(Name = "Дата съёмки")]
    public DateTime TakenAt { get; set; } = DateTime.Today;
}

public class HomeCareFormModel
{
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Укажите название")]
    [Display(Name = "Название")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Инструкции")]
    public string? Instructions { get; set; }

    [Display(Name = "Средства")]
    [StringLength(1000)]
    public string? Products { get; set; }

    [Display(Name = "Дата назначения")]
    public DateTime PrescribedAt { get; set; } = DateTime.Today;
}
