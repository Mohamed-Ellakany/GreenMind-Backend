using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using Greenmind.Domain.Entities;
using Greenmind.Domain.Consts;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Api.Seed;

public static class SeedExtensions
{
    public static async Task EnsureRolesAndAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<GreenmindDbContext>();

        var roles = new[] { RoleNames.Admin, RoleNames.SuperAdmin, RoleNames.FarmOwner };
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
                await userManager.AddToRoleAsync(admin, RoleNames.Admin);
                await userManager.AddToRoleAsync(admin, RoleNames.SuperAdmin);
            }
        }
        if (!await userManager.IsInRoleAsync(admin, RoleNames.SuperAdmin))
            await userManager.AddToRoleAsync(admin, RoleNames.SuperAdmin);
        else if (!await userManager.IsInRoleAsync(admin, RoleNames.Admin))
        {
            await userManager.AddToRoleAsync(admin, RoleNames.Admin);
            if (await userManager.IsInRoleAsync(admin, "PlatformAdmin"))
                await userManager.RemoveFromRoleAsync(admin, "PlatformAdmin");
        }

        var farms = await dbContext.Farms
            .AsNoTracking()
            .Select(farm => farm.Id)
            .ToListAsync();

        var existingCategories = await dbContext.ExpenseCategories
            .AsNoTracking()
            .Select(category => new { category.FarmId, category.Name })
            .ToListAsync();

        var existing = existingCategories
            .Select(category => (category.FarmId, category.Name))
            .ToHashSet();

        foreach (var farmId in farms)
        {
            foreach (var name in DefaultExpenseCategories.ArabicNames)
            {
                if (existing.Add((farmId, name)))
                {
                    dbContext.ExpenseCategories.Add(new ExpenseCategory
                    {
                        FarmId = farmId,
                        Name = name,
                        IsDefault = true
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
