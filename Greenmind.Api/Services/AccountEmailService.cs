using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Greenmind.Api.Services;

public sealed class AccountEmailService : IAccountEmailService
{
    private const string DefaultGmailHost = "smtp.gmail.com";
    private const int DefaultPort = 587;
    private const string DefaultDisplayName = "GreenMind | نظام العقل الأخضر";

    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountEmailService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    private static byte[]? _cachedLogoBytes;
    private static readonly object _logoLock = new();

    public AccountEmailService(
        IConfiguration configuration,
        ILogger<AccountEmailService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsConfigured
    {
        get
        {
            var settings = _configuration.GetSection("Smtp");
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
            _logger.LogInformation("No active admin email addresses found for farm registration alert {FarmId}.", farmId);
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

        var settings = _configuration.GetSection("Smtp");
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

        var htmlView = AlternateView.CreateAlternateViewFromString(
            htmlBody,
            Encoding.UTF8,
            MediaTypeNames.Text.Html);

        // Attach logo inline via MIME LinkedResource with Content-ID: <greenmind-logo>
        MemoryStream? logoStream = null;
        var logoBytes = GetLogoBytes();
        if (logoBytes is { Length: > 0 })
        {
            logoStream = new MemoryStream(logoBytes);
            var logoResource = new LinkedResource(logoStream, "image/png")
            {
                ContentId = "greenmind-logo",
                TransferEncoding = TransferEncoding.Base64
            };
            logoResource.ContentType.MediaType = "image/png";
            logoResource.ContentType.Name = "logo.png";
            htmlView.LinkedResources.Add(logoResource);
        }

        message.AlternateViews.Add(htmlView);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Successfully sent email '{Subject}' to {Recipient} via {Host}:{Port}.", subject, to, host, port);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Failed to send email '{Subject}' to {Recipient} via {Host}:{Port}. Verify your Gmail App Password and 2-Step Verification settings.", subject, to, host, port);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email '{Subject}' to {Recipient}.", subject, to);
            throw;
        }
        finally
        {
            logoStream?.Dispose();
        }
    }

    private string FrontendBaseUrl
    {
        get
        {
            // 1. Explicit configuration or environment variable (e.g. Frontend:PublicUrl or FRONTEND_URL)
            var configUrl = _configuration["Frontend:PublicUrl"]
                ?? _configuration["FrontendUrl"]
                ?? _configuration["FRONTEND_URL"]
                ?? _configuration["App:FrontendUrl"];

            if (!string.IsNullOrWhiteSpace(configUrl))
            {
                var trimmed = configUrl.Trim().TrimEnd('/');
                // If explicitly set to a remote domain (not default localhost), prioritize it
                if (!trimmed.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed;
                }
            }

            // 2. Dynamic detection from current HTTP Request (Origin / Referer / Host / Forwarded headers)
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext is not null)
            {
                // Frontend Origin header
                var origin = httpContext.Request.Headers.Origin.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    return $"{originUri.Scheme}://{originUri.Authority}".TrimEnd('/');
                }

                // Frontend Referer header
                var referer = httpContext.Request.Headers.Referer.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                {
                    return $"{refererUri.Scheme}://{refererUri.Authority}".TrimEnd('/');
                }

                // Reverse proxy or host header
                var scheme = httpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? httpContext.Request.Scheme;
                var host = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? httpContext.Request.Host.Value;
                if (!string.IsNullOrWhiteSpace(host))
                {
                    return $"{scheme}://{host}".TrimEnd('/');
                }
            }

            // 3. Fallback to configured value if present
            if (!string.IsNullOrWhiteSpace(configUrl))
            {
                return configUrl.Trim().TrimEnd('/');
            }

            // 4. Default fallback
            return "http://localhost:4200";
        }
    }

    private string FrontendUrl(string path) =>
        FrontendBaseUrl + (path.StartsWith('/') ? path : "/" + path);

    private static byte[]? GetLogoBytes()
    {
        if (_cachedLogoBytes is not null)
            return _cachedLogoBytes;

        lock (_logoLock)
        {
            if (_cachedLogoBytes is not null)
                return _cachedLogoBytes;

            // 1. Try disk paths
            var candidatePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "logo.png"),
                Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo.png")
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    _cachedLogoBytes = File.ReadAllBytes(path);
                    return _cachedLogoBytes;
                }
            }

            // 2. Try Embedded Resource
            var assembly = typeof(AccountEmailService).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            var logoResourceName = resourceNames.FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
            if (logoResourceName is not null)
            {
                using var stream = assembly.GetManifestResourceStream(logoResourceName);
                if (stream is not null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    _cachedLogoBytes = ms.ToArray();
                    return _cachedLogoBytes;
                }
            }

            return null;
        }
    }
}

