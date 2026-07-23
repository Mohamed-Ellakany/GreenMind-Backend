using Greenmind.Api.Filters;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Api.IntegrationTests;

public class GreenmindWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("InMemoryDatabaseName", _databaseName);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.AddControllers()
                .AddApplicationPart(typeof(TestFarmOwnerController).Assembly);

            services.AddScoped<ActiveFarmRequiredFilter>();
        });
    }

    public async System.Threading.Tasks.Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task<(ApplicationUser Owner, Farm Farm)> SeedFarmAsync(
        FarmStatus status,
        bool ownerIsActive = false,
        string farmName = "Test Farm")
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();

        var owner = new ApplicationUser
        {
            UserName = $"owner-{Guid.NewGuid()}@test.com",
            Email = $"owner-{Guid.NewGuid()}@test.com",
            FullName = "Test Owner",
            IsActive = ownerIsActive
        };

        dbContext.Users.Add(owner);
        await dbContext.SaveChangesAsync();

        var farm = new Farm
        {
            Name = farmName,
            OwnerUserId = owner.Id,
            Status = status
        };

        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync();

        return (owner, farm);
    }

    public HttpClient CreateAuthenticatedClient(int userId, string role)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add(TestAuthDefaults.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthDefaults.RoleHeader, role);

        return client;
    }
}
