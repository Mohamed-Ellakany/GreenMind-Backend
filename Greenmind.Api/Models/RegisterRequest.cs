using System.ComponentModel.DataAnnotations;

namespace Greenmind.Api.Models;

public class RegisterRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string FarmName { get; set; } = null!;
}
