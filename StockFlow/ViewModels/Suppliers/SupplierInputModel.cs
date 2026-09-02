using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Suppliers;

public sealed class SupplierInputModel
{
    [Display(Name = "Şirket adı")]
    [Required(ErrorMessage = "Şirket adı zorunludur.")]
    [StringLength(200, ErrorMessage = "Şirket adı en fazla 200 karakter olabilir.")]
    public string CompanyName { get; set; } = string.Empty;

    [Display(Name = "E-posta")]
    [StringLength(256, ErrorMessage = "E-posta adresi en fazla 256 karakter olabilir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girilmelidir.")]
    public string? Email { get; set; }

    [Display(Name = "Telefon")]
    [StringLength(32, ErrorMessage = "Telefon numarası en fazla 32 karakter olabilir.")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası girilmelidir.")]
    public string? Phone { get; set; }

    [Display(Name = "Adres")]
    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    public string? Address { get; set; }
}
