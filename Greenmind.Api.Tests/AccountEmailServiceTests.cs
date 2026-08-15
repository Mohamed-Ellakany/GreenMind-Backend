using Greenmind.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Greenmind.Api.Tests;

public sealed class AccountEmailServiceTests
{
    [Fact]
    public void IsConfigured_ReturnsTrue_WhenCredentialsAreProvided()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:Port"] = "587",
            ["Smtp:UserName"] = "greenmind.test@gmail.com",
            ["Smtp:Password"] = "abcd efgh ijkl mnop",
            ["Smtp:From"] = "greenmind.test@gmail.com"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var service = new AccountEmailService(configuration, NullLogger<AccountEmailService>.Instance);

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenUsingApiKeyAndDefaultFrom()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Smtp:UserName"] = "greenmind.test@gmail.com",
            ["Smtp:ApiKey"] = "abcdefghijklmnop"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var service = new AccountEmailService(configuration, NullLogger<AccountEmailService>.Instance);

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenCredentialsAreMissing()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.gmail.com",
            ["Smtp:UserName"] = "",
            ["Smtp:Password"] = ""
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var service = new AccountEmailService(configuration, NullLogger<AccountEmailService>.Instance);

        Assert.False(service.IsConfigured);
    }

    [Fact]
    public void BuildAdminInvitationEmail_GeneratesRichHtmlAndPlainText()
    {
        var html = EmailTemplateBuilder.BuildAdminInvitationEmail(
            fullName: "Ahmed & Ali",
            email: "ahmed@example.com",
            actionUrl: "https://greenmind.local/auth/accept-invitation?token=secret123",
            frontendBaseUrl: "https://greenmind.local");

        Assert.Contains("GreenMind", html);
        Assert.Contains("Ahmed &amp; Ali", html);
        Assert.Contains("ahmed@example.com", html);
        Assert.Contains("https://greenmind.local/auth/accept-invitation?token=secret123", html);
        Assert.Contains("Platform Admin", html);
        Assert.Contains("7 Days", html);

        var plainText = EmailTemplateBuilder.StripHtmlForPlainText(html);
        Assert.Contains("Ahmed & Ali", plainText);
        Assert.Contains("https://greenmind.local/auth/accept-invitation?token=secret123", plainText);
        Assert.DoesNotContain("<table", plainText);
    }

    [Fact]
    public void BuildRegistrationReceivedEmail_GeneratesExpectedContent()
    {
        var html = EmailTemplateBuilder.BuildRegistrationReceivedEmail(
            fullName: "Farmer John",
            farmName: "Sunrise Olive Estate",
            loginUrl: "https://greenmind.local/auth/login",
            frontendBaseUrl: "https://greenmind.local");

        Assert.Contains("Farmer John", html);
        Assert.Contains("Sunrise Olive Estate", html);
        Assert.Contains("Under Review", html);
        Assert.Contains("https://greenmind.local/auth/login", html);
    }

    [Fact]
    public void BuildNewRegistrationAlertEmail_GeneratesAdminAlert()
    {
        var html = EmailTemplateBuilder.BuildNewRegistrationAlertEmail(
            adminFullName: "Admin",
            ownerFullName: "Hassan Mahmoud",
            ownerEmail: "hassan@farm.eg",
            farmName: "Nile Valley Orchards",
            farmId: 104,
            reviewUrl: "https://greenmind.local/admin-dashboard/accounts",
            frontendBaseUrl: "https://greenmind.local");

        Assert.Contains("Hassan Mahmoud", html);
        Assert.Contains("hassan@farm.eg", html);
        Assert.Contains("Nile Valley Orchards", html);
        Assert.Contains("#104", html);
        Assert.Contains("https://greenmind.local/admin-dashboard/accounts", html);
    }

    [Fact]
    public void BuildFarmApprovedEmail_ContainsHighlightsAndDashboardUrl()
    {
        var html = EmailTemplateBuilder.BuildFarmApprovedEmail(
            fullName: "Nour Eldin",
            farmName: "Delta Berry Farms",
            dashboardUrl: "https://greenmind.local/dashboard",
            frontendBaseUrl: "https://greenmind.local");

        Assert.Contains("Nour Eldin", html);
        Assert.Contains("Delta Berry Farms", html);
        Assert.Contains("AI Export Planning", html);
        Assert.Contains("https://greenmind.local/dashboard", html);
    }

    [Fact]
    public void BuildAccountActivationEmail_GeneratesExpectedContent()
    {
        var html = EmailTemplateBuilder.BuildAccountActivationEmail(
            fullName: "Farmer John",
            email: "farmer@greenmind.local",
            activationUrl: "https://greenmind.local/auth/activate-account?email=farmer%40greenmind.local&token=act123",
            frontendBaseUrl: "https://greenmind.local");

        Assert.Contains("Farmer John", html);
        Assert.Contains("farmer@greenmind.local", html);
        Assert.Contains("Activate Account", html);
        Assert.Contains("https://greenmind.local/auth/activate-account?email=farmer%40greenmind.local&amp;token=act123", html);
        Assert.Contains("Security Notice", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPasswordResetEmail_ContainsResetLinkAndSecurityNotice()
    {
        var html = EmailTemplateBuilder.BuildPasswordResetEmail(
            fullName: "Sarah Connor",
            email: "sarah@cyber.local",
            resetUrl: "https://greenmind.local/auth/reset-password?email=sarah%40cyber.local&token=tok456",
            frontendBaseUrl: "https://greenmind.local");

        Assert.Contains("Sarah Connor", html);
        Assert.Contains("sarah@cyber.local", html);
        Assert.Contains("1 Hour", html);
        Assert.Contains("https://greenmind.local/auth/reset-password?email=sarah%40cyber.local&amp;token=tok456", html);
        Assert.Contains("Security Notice", html, StringComparison.OrdinalIgnoreCase);
    }
}
