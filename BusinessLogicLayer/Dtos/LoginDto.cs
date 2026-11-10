using System.ComponentModel.DataAnnotations;

namespace BusinessLogicLayer.Dtos;

public class LoginDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(1)]
    public string Password { get; set; } = string.Empty;
}