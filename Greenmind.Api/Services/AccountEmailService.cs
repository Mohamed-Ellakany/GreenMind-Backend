using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Greenmind.Api.Services;

public sealed class AccountEmailService(
    IConfiguration configuration,
    ILogger<AccountEmailService> logger) : IAccountEmailService
{
    private const string DefaultGmailHost = "smtp.gmail.com";
    private const int DefaultPort = 587;
    private const string DefaultDisplayName = "GreenMind | نظام العقل الأخضر";

    public bool IsConfigured
    {
        get
        {
            var settings = configuration.GetSection("Smtp");
            var userName = settings["UserName"]?.Trim();
            var password = (settings["Password"] ?? settings["ApiKey"])?.Trim();
            var from = (settings["From"] ?? userName)?.Trim();

            return !string.IsNullOrWhiteSpace(userName)
                && !string.IsNullOrWhiteSpace(password)
                && !string.IsNullOrWhiteSpace(from);
        }
    }

    public Task SendAdminInvitationAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var actionUrl = FrontendUrl("/auth/accept-invitation?token=" + Uri.EscapeDataString(token));
        var htmlBody = EmailTemplateBuilder.BuildAdminInvitationEmail(
            fullName: fullName,
            email: email,
            actionUrl: actionUrl,
            frontendBaseUrl: FrontendBaseUrl);

        return SendAsync(
            to: email,
            subject: "دعوة للانضمام إلى منصة GreenMind | Admin Invitation",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    public Task SendAccountActivationAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var activationUrl = FrontendUrl("/auth/activate-account?email=" + Uri.EscapeDataString(email) + "&token=" + Uri.EscapeDataString(token));
        var htmlBody = EmailTemplateBuilder.BuildAccountActivationEmail(
            fullName: fullName,
            email: email,
            activationUrl: activationUrl,
            frontendBaseUrl: FrontendBaseUrl);

        return SendAsync(
            to: email,
            subject: "تفعيل حسابك في منصة جرين مايند | Activate Your GreenMind Account",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    public Task SendRegistrationReceivedAsync(
        string email,
        string fullName,
        string farmName,
        CancellationToken cancellationToken = default)
    {
        var loginUrl = FrontendUrl("/auth/login");
        var htmlBody = EmailTemplateBuilder.BuildRegistrationReceivedEmail(
            fullName: fullName,
            farmName: farmName,
            loginUrl: loginUrl,
            frontendBaseUrl: FrontendBaseUrl);

        return SendAsync(
            to: email,
            subject: "تم استلام طلب تسجيل المزرعة | GreenMind Registration Received",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    public async Task SendNewRegistrationAlertAsync(
        IEnumerable<string> adminEmails,
        string fullName,
        string email,
        string farmName,
        int farmId,
        CancellationToken cancellationToken = default)
    {
        var recipients = adminEmails
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            logger.LogInformation("No active admin email addresses found for farm registration alert {FarmId}.", farmId);
            return;
        }

        var reviewUrl = FrontendUrl("/admin-dashboard/accounts");
        var htmlBody = EmailTemplateBuilder.BuildNewRegistrationAlertEmail(
            adminFullName: "Admin",
            ownerFullName: fullName,
            ownerEmail: email,
            farmName: farmName,
            farmId: farmId,
            reviewUrl: reviewUrl,
            frontendBaseUrl: FrontendBaseUrl);

        foreach (var recipient in recipients)
        {
            await SendAsync(
                to: recipient,
                subject: $"طلب تسجيل مزرعة جديدة (#{farmId}) | New Farm Registration",
                htmlBody: htmlBody,
                cancellationToken: cancellationToken);
        }
    }

    public Task SendFarmApprovedAsync(
        string email,
        string fullName,
        string farmName,
        CancellationToken cancellationToken = default)
    {
        var dashboardUrl = FrontendUrl("/dashboard");
        var htmlBody = EmailTemplateBuilder.BuildFarmApprovedEmail(
            fullName: fullName,
            farmName: farmName,
            dashboardUrl: dashboardUrl,
            frontendBaseUrl: FrontendBaseUrl);

        return SendAsync(
            to: email,
            subject: "تهانينا! تم اعتماد وتفعيل حساب مزرعتك | GreenMind Farm Approved",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var resetUrl = FrontendUrl("/auth/reset-password?email=" + Uri.EscapeDataString(email) + "&token=" + Uri.EscapeDataString(token));
        var htmlBody = EmailTemplateBuilder.BuildPasswordResetEmail(
            fullName: fullName,
            email: email,
            resetUrl: resetUrl,
            frontendBaseUrl: FrontendBaseUrl);

        return SendAsync(
            to: email,
            subject: "إعادة تعيين كلمة المرور | GreenMind Password Reset",
            htmlBody: htmlBody,
            cancellationToken: cancellationToken);
    }

    private async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gmail / SMTP credentials are not configured. Please set Smtp:UserName and Smtp:Password in appsettings.json or user secrets.");
        }

        var settings = configuration.GetSection("Smtp");
        var host = string.IsNullOrWhiteSpace(settings["Host"]) ? DefaultGmailHost : settings["Host"]!.Trim();
        var port = settings.GetValue("Port", DefaultPort);
        var useSsl = settings.GetValue("UseSsl", true);
        var userName = settings["UserName"]!.Trim();
        
        // Sanitize password: Google App Passwords are often generated as 4-character blocks separated by spaces (e.g., 'abcd efgh ijkl mnop').
        var rawPassword = (settings["Password"] ?? settings["ApiKey"]) ?? string.Empty;
        var password = rawPassword.Replace(" ", string.Empty).Trim();

        var fromAddress = string.IsNullOrWhiteSpace(settings["From"]) ? userName : settings["From"]!.Trim();
        var displayName = string.IsNullOrWhiteSpace(settings["DisplayName"]) ? DefaultDisplayName : settings["DisplayName"]!.Trim();

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(userName, password),
            Timeout = 30000
        };

        var from = new MailAddress(fromAddress, displayName, Encoding.UTF8);
        using var message = new MailMessage
        {
            From = from,
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(to);

        var plainText = EmailTemplateBuilder.StripHtmlForPlainText(htmlBody);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            plainText,
            Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            htmlBody,
            Encoding.UTF8,
            MediaTypeNames.Text.Html));

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Successfully sent email '{Subject}' to {Recipient} via {Host}:{Port}.", subject, to, host, port);
        }
        catch (SmtpException ex)
        {
            logger.LogError(ex, "Failed to send email '{Subject}' to {Recipient} via {Host}:{Port}. Verify your Gmail App Password and 2-Step Verification settings.", subject, to, host, port);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error sending email '{Subject}' to {Recipient}.", subject, to);
            throw;
        }
    }

    private string FrontendBaseUrl =>
        (configuration["Frontend:PublicUrl"] ?? "http://localhost:4200").TrimEnd('/');

    private string FrontendUrl(string path) =>
        FrontendBaseUrl + (path.StartsWith('/') ? path : "/" + path);
}
