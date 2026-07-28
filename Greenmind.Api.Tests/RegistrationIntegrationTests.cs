using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Api.Models;
using Xunit;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Greenmind.Application.Contracts.Api;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Greenmind.Api.Tests;

public class RegistrationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegistrationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<GreenmindDbContext>();
                services.RemoveAll<DbContextOptions<GreenmindDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<GreenmindDbContext>>();

                services.AddDbContext<GreenmindDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb")
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
    }

    [Fact]
    public async Task Register_CreatesInactiveFarmOwnerAndFarm()
    {
        var client = _factory.CreateClient();

        var request = new RegisterRequest
        {
            FullName = "Test Owner",
            Email = "owner@test.local",
            Password = "P@ssword123",
            FarmName = "Test Farm"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        Assert.NotNull(created?.Data);

        // verify DB state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
        Assert.NotNull(user);
        Assert.False(user!.IsActive);

        var farm = await db.Farms.SingleOrDefaultAsync(f => f.OwnerUserId == user.Id);
        Assert.NotNull(farm);
        Assert.Equal(request.FarmName, farm!.Name);
    }
}
