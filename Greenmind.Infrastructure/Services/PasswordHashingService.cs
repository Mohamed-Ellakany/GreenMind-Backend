using Microsoft.AspNetCore.Identity;
using Greenmind.Application.Services;
using Greenmind.Domain.Entities;

namespace Greenmind.Infrastructure.Services;

public class PasswordHashingService : IPasswordHashingService
{
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public PasswordHashingService(IPasswordHasher<ApplicationUser> hasher)
    {
        _hasher = hasher;
    }

    public string Hash(string password)
    {
        // We create a throwaway user instance because ASP.NET Core's IPasswordHasher requires a user instance
        var user = new ApplicationUser { FullName = string.Empty };
        return _hasher.HashPassword(user, password);
    }

    public bool Verify(string hashed, string providedPassword)
    {
        var user = new ApplicationUser { FullName = string.Empty };
        var result = _hasher.VerifyHashedPassword(user, hashed, providedPassword);
        return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
