namespace Greenmind.Api.Models;

public class LoginResponse
{
    public string Token { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
}
