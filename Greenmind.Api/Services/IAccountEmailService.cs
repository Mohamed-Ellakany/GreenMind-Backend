namespace Greenmind.Api.Services;

public interface IAccountEmailService
{
    bool IsConfigured { get; }

    Task SendAdminInvitationAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default);

    Task SendRegistrationReceivedAsync(
        string email,
        string fullName,
        string farmName,
        CancellationToken cancellationToken = default);

    Task SendNewRegistrationAlertAsync(
        IEnumerable<string> adminEmails,
        string fullName,
        string email,
        string farmName,
        int farmId,
        CancellationToken cancellationToken = default);

    Task SendFarmApprovedAsync(
        string email,
        string fullName,
        string farmName,
        CancellationToken cancellationToken = default);

    Task SendAccountActivationAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, string fullName, string token, CancellationToken cancellationToken = default);
}
