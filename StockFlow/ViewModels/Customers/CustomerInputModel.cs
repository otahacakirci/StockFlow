using System.ComponentModel.DataAnnotations;

namespace StockFlow.ViewModels.Customers;

public sealed class CustomerInputModel
{
    [Display(Name = "Müşteri adı")]
    [Required(ErrorMessage = "Müşteri adı zorunludur.")]
    [StringLength(150, ErrorMessage = "Müşteri adı en fazla 150 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

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
