using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Greenmind.Domain.Entities;

namespace Greenmind.Api.Seed;

public static class SeedExtensions
{
    public static async Task EnsureRolesAndAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "PlatformAdmin", "FarmOwner" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new IdentityRole(r));
        }

        var adminEmail = "admin@local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Platform Admin",
                IsActive = true
            };

            var pw = "ChangeMe!P@ssw0rd"; // override in production
            var createRes = await userManager.CreateAsync(admin, pw);
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "PlatformAdmin");
            }
        }
    }
}
