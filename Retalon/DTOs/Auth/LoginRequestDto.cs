using System.ComponentModel.DataAnnotations;

namespace Retalon.DTOs.Auth;

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

}