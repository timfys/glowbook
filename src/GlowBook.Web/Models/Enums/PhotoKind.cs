using System.ComponentModel.DataAnnotations;

namespace GlowBook.Web.Models.Enums;

public enum PhotoKind
{
    [Display(Name = "До")]
    Before = 0,

    [Display(Name = "После")]
    After = 1
}
