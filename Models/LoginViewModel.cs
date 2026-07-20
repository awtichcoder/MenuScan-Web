using System.ComponentModel.DataAnnotations;

namespace MenuQr.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui long nhap ten dang nhap.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui long nhap mat khau.")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
