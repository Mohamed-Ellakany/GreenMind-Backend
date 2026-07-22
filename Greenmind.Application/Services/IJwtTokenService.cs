using System.Security.Claims;

namespace Greenmind.Application.Services;

public interface IJwtTokenService
{
    string CreateToken(IEnumerable<Claim> claims);
}
