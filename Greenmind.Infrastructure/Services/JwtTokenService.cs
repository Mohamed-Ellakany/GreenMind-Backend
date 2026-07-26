using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Greenmind.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Greenmind.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string CreateToken(IEnumerable<Claim> claims)
    {
        var key = _config["Jwt:Key"] ?? "dev_secret_key_change_me";
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        var expiresMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        var keyBytes = Encoding.UTF8.GetBytes(key);
        // Ensure key is at least 256 bits (32 bytes) for HS256. If shorter, expand deterministically.
        if (keyBytes.Length < 32)
        {
            var expanded = new byte[32];
            for (int i = 0; i < expanded.Length; i++)
            {
                expanded[i] = keyBytes[i % keyBytes.Length];
            }
            keyBytes = expanded;
        }

        var securityKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
