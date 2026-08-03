using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Encodings.Web;

namespace Greenmind.Api.Services;

public sealed class AccountEmailService(
    IConfiguration configuration,
    ILogger<AccountEmailService> logger) : IAccountEmailService
{
    public bool IsConfigured
    {
        get
        {
            var settings = configuration.GetSection("Smtp");
            return !string.IsNullOrWhiteSpace(settings["Host"])
                && !string.IsNullOrWhiteSpace(settings["UserName"])
                && !string.IsNullOrWhiteSpace(settings["Password"])
                && !string.IsNullOrWhiteSpace(settings["From"]);
        }
    }

    public Task SendAdminInvitationAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var url = FrontendUrl("/auth/accept-invitation?token=" + Uri.EscapeDataString(token));
        var body = BuildEmail(
            "You have been invited to GreenMind",
            $"Hello {Encode(fullName)},",
            "A super admin invited you to help manage GreenMind. Create your password to activate your admin account.",
            "Create admin account",
            url,
            [
                ("Invitation expires", "7 days"),
                ("Account role", "Admin")
            ]);

        return SendAsync(email, "GreenMind admin invitation", body, cancellationToken);
    }

    public Task SendRegistrationReceivedAsync(
        string email,
        string fullName,
        string farmName,
        CancellationToken cancellationToken = default)
    {
        var body = BuildEmail(
            "Your GreenMind request is under review",
            $"Hello {Encode(fullName)},",
            $"Thanks for registering {Encode(farmName)} with GreenMind. We received your request and our admin team is reviewing the farm details.",
            "Open GreenMind",
            FrontendUrl("/auth/login"),
            [
                ("Current status", "Waiting for admin approval"),
                ("Next step", "We will email you as soon as your account is approved")
            ]);

        return SendAsync(email, "GreenMind registration received", body, cancellationToken);
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

        var body = BuildEmail(
            "New farm account needs approval",
            "Hello GreenMind admin,",
            $"{Encode(fullName)} registered {Encode(farmName)} and is waiting for account approval.",
            "Review farm accounts",
            FrontendUrl("/admin-dashboard/accounts"),
            [
                ("Farm", Encode(farmName)),
                ("Owner", Encode(fullName)),
                ("Email", Encode(email)),
                ("Farm ID", farmId.ToString())
            ]);

        foreach (var recipient in recipients)
        {
            await SendAsync(recipient, "New GreenMind farm registration", body, cancellationToken);
        }
    }

    public Task SendFarmApprovedAsync(
        string email,
        string fullName,
        string farmName,
        CancellationToken cancellationToken = default)
    {
        var body = BuildEmail(
            "Your GreenMind account is approved",
            $"Welcome, {Encode(fullName)}.",
            $"{Encode(farmName)} is now active. You can sign in and start building export plans, tracking tasks, and managing farm data from your dashboard.",
            "Go to dashboard",
            FrontendUrl("/dashboard"),
            [
                ("Account status", "Approved and active"),
                ("Farm", Encode(farmName))
            ]);

        return SendAsync(email, "Your GreenMind account is approved", body, cancellationToken);
    }

    private async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("SMTP credentials are not configured.");
        }

        var settings = configuration.GetSection("Smtp");
        using var client = new SmtpClient(settings["Host"], settings.GetValue("Port", 587))
        {
            EnableSsl = settings.GetValue("UseSsl", true),
            Credentials = new NetworkCredential(settings["UserName"], settings["Password"])
        };

        using var message = new MailMessage
        {
            From = new MailAddress(settings["From"]!),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(to);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            StripHtmlForPlainText(htmlBody),
            Encoding.UTF8,
            MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            htmlBody,
            Encoding.UTF8,
            MediaTypeNames.Text.Html));

        await client.SendMailAsync(message, cancellationToken);
    }

    private string FrontendUrl(string path) =>
        (configuration["Frontend:PublicUrl"] ?? "http://localhost:4200").TrimEnd('/') + path;

    private static string BuildEmail(
        string title,
        string greeting,
        string body,
        string actionLabel,
        string actionUrl,
        IReadOnlyList<(string Label, string Value)> details)
    {
        var detailRows = string.Join("", details.Select(item => $"""
            <tr>
              <td style="padding:10px 0;color:#65736c;font-size:13px;">{item.Label}</td>
              <td style="padding:10px 0;color:#10231b;font-size:13px;font-weight:700;text-align:right;">{item.Value}</td>
            </tr>
            """));

        return $"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;background:#f4f7f4;font-family:Arial,'Segoe UI',sans-serif;color:#10231b;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7f4;padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #dde6df;border-radius:20px;overflow:hidden;">
                      <tr>
                        <td style="background:#10231b;padding:26px 30px;">
                          <div style="color:#d8f36a;font-size:12px;font-weight:800;letter-spacing:.08em;text-transform:uppercase;">GreenMind</div>
                          <h1 style="margin:10px 0 0;color:#ffffff;font-size:25px;line-height:1.25;">{Encode(title)}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:30px;">
                          <p style="margin:0 0 12px;font-size:16px;font-weight:700;">{greeting}</p>
                          <p style="margin:0;color:#526158;font-size:15px;line-height:1.7;">{body}</p>
                          <p style="margin:28px 0;">
                            <a href="{Encode(actionUrl)}" style="display:inline-block;border-radius:12px;background:#0f7a3d;color:#ffffff;font-size:14px;font-weight:800;text-decoration:none;padding:13px 18px;">{Encode(actionLabel)}</a>
                          </p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-top:1px solid #edf1ee;border-bottom:1px solid #edf1ee;margin-top:8px;">
                            {detailRows}
                          </table>
                          <p style="margin:22px 0 0;color:#8a968f;font-size:12px;line-height:1.6;">This message was sent automatically by GreenMind.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);

    private static string StripHtmlForPlainText(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Trim();
}
