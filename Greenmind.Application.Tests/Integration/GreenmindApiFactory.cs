using System.Security.Claims;
using System.Text.Encodings.Web;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Greenmind.Application.Tests.Integration;

public sealed class GreenmindApiFactory : WebApplicationFactory<Greenmind.Api.Program>, IAsyncLifetime
{
    public const string AdminUserId = "integration-admin";
    public const string FarmOwnerUserId = "integration-farm-owner";
    public const string OtherFarmOwnerUserId = "integration-other-farm-owner";
    public const string AuthenticationScheme = "IntegrationTest";

    private readonly string _databaseName = $"greenmind-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<GreenmindDbContext>();
            services.RemoveAll<DbContextOptions<GreenmindDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<GreenmindDbContext>>();

            services.AddDbContext<GreenmindDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddDataProtection().UseEphemeralDataProtectionProvider();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AuthenticationScheme;
                    options.DefaultChallengeScheme = AuthenticationScheme;
                    options.DefaultForbidScheme = AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                    AuthenticationScheme,
                    _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Users.AddRange(
            new ApplicationUser
            {
                Id = AdminUserId,
                UserName = "integration.admin",
                NormalizedUserName = "INTEGRATION.ADMIN",
                FullName = "Integration Admin",
                IsActive = true
            },
            new ApplicationUser
            {
                Id = FarmOwnerUserId,
                UserName = "integration.owner",
                NormalizedUserName = "INTEGRATION.OWNER",
                FullName = "Integration Farm Owner",
                IsActive = true
            },
            new ApplicationUser
            {
                Id = OtherFarmOwnerUserId,
                UserName = "integration.other-owner",
                NormalizedUserName = "INTEGRATION.OTHER-OWNER",
                FullName = "Integration Other Farm Owner",
                IsActive = true
            });

        dbContext.Farms.Add(new Farm
        {
            Name = "Integration Farm",
            OwnerUserId = FarmOwnerUserId,
            IsApproved = true,
            ApprovedAtUtc = DateTime.UtcNow
        });

        dbContext.Farms.Add(new Farm
        {
            Name = "Integration Other Farm",
            OwnerUserId = OtherFarmOwnerUserId,
            IsApproved = true,
            ApprovedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}

internal sealed class IntegrationTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].FirstOrDefault();
        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
            new(ClaimTypes.Role, role)
        ];

        var identity = new ClaimsIdentity(claims, GreenmindApiFactory.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, GreenmindApiFactory.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
