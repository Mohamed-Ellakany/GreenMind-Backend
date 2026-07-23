namespace Greenmind.Application.Services;

public interface IPasswordHashingService
{
    string Hash(string password);
    bool Verify(string hashed, string providedPassword);
}
