using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Api.Models;
using Xunit;

namespace Greenmind.Api.Tests;

public class RegistrationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegistrationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GreenmindDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<GreenmindDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
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

        var created = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(created);

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
