using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Api.Models;
using Greenmind.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using Xunit;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Greenmind.Application.Contracts.Api;
using Greenmind.Api.Controllers;

namespace Greenmind.Api.Tests;

public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("AuthTestDb"));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
    }

    [Fact]
    public async Task ActiveUser_CanLogin_AndTokenContainsClaims()
    {
        var client = _factory.CreateClient();

        // create active user
        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var db = services.GetRequiredService<GreenmindDbContext>();

            if (!await roleManager.RoleExistsAsync("FarmOwner"))
                await roleManager.CreateAsync(new IdentityRole("FarmOwner"));

            var user = new ApplicationUser
            {
                UserName = "active@test.local",
                Email = "active@test.local",
                FullName = "Active User",
                IsActive = true,
                EmailConfirmed = true
            };
            var res = await userManager.CreateAsync(user, "P@ssword123");
            await userManager.AddToRoleAsync(user, "FarmOwner");

            var farm = new Farm { Name = "Active Farm", OwnerUserId = user.Id, IsApproved = true };
            db.Farms.Add(farm);
            await db.SaveChangesAsync();
        }

        var login = new LoginRequest { Email = "active@test.local", Password = "P@ssword123" };
        var response = await client.PostAsJsonAsync("/api/auth/login", login);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.NotNull(body?.Data);
        Assert.False(string.IsNullOrWhiteSpace(body!.Data!.Token));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(body.Data.Token);

        var sub = token.Claims.SingleOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        Assert.NotNull(sub);

        var role = token.Claims.SingleOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role);
        Assert.NotNull(role);
        Assert.Equal("FarmOwner", role!.Value);

        var farmId = token.Claims.SingleOrDefault(c => c.Type == "farmId");
        Assert.NotNull(farmId);
    }

    [Fact]
    public async Task InactiveUser_WithValidPassword_GetsInactiveCode()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var db = services.GetRequiredService<GreenmindDbContext>();

            var user = new ApplicationUser
            {
                UserName = "inactive@test.local",
                Email = "inactive@test.local",
                FullName = "Inactive User",
                IsActive = false,
                EmailConfirmed = false
            };
            await userManager.CreateAsync(user, "P@ssword123");
            db.Farms.Add(new Farm { Name = "Inactive Farm", OwnerUserId = user.Id, IsApproved = true });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "inactive@test.local", Password = "P@ssword123" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ApiErrorCodes.AccountInactive, body?.Error?.Code);
    }

    [Fact]
    public async Task ActivateAccount_WithValidToken_ActivatesUserAndAllowsLogin()
    {
        var client = _factory.CreateClient();
        string email = "activate-me@test.local";
        string password = "P@ssword123";
        string token;

        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var db = services.GetRequiredService<GreenmindDbContext>();

            if (!await roleManager.RoleExistsAsync("FarmOwner"))
                await roleManager.CreateAsync(new IdentityRole("FarmOwner"));

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "To Activate",
                IsActive = false,
                EmailConfirmed = false
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, "FarmOwner");

            db.Farms.Add(new Farm { Name = "Activate Farm", OwnerUserId = user.Id, IsApproved = true });
            await db.SaveChangesAsync();

            token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        // Activate
        var activateResponse = await client.PostAsJsonAsync(
            "/api/auth/activate-account",
            new ActivateAccountRequest(email, token));
        activateResponse.EnsureSuccessStatusCode();

        // Now login should succeed
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        Assert.NotNull(body?.Data?.Token);
    }

    [Fact]
    public async Task InactiveUser_WithInvalidPassword_DoesNotRevealActivationState()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var db = services.GetRequiredService<GreenmindDbContext>();

            var user = new ApplicationUser
            {
                UserName = "unconfirmed-invalid@test.local",
                Email = "unconfirmed-invalid@test.local",
                FullName = "Unconfirmed Invalid User",
                IsActive = false,
                EmailConfirmed = false
            };
            await userManager.CreateAsync(user, "P@ssword123");
            db.Farms.Add(new Farm { Name = "Unconfirmed Farm", OwnerUserId = user.Id, IsApproved = true });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "unconfirmed-invalid@test.local", Password = "wrong-password" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ApiErrorCodes.Unauthorized, body?.Error?.Code);
    }
}
