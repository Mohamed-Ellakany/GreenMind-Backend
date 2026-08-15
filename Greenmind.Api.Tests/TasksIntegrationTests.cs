using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Domain.Entities;
using Greenmind.Api.Models;
using Microsoft.AspNetCore.Identity;
using Xunit;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Greenmind.Application.Contracts.Api;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenmind.Api.Tests;

public class TasksIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TasksIntegrationTests(WebApplicationFactory<Program> factory)
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
                services.AddDbContext<GreenmindDbContext>(opts => opts.UseInMemoryDatabase("TasksTestDb"));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
    }

    private async Task<(string Token, int TaskId)> CreateAndLoginOwnerAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<GreenmindDbContext>();

        if (!await roleManager.RoleExistsAsync("FarmOwner")) await roleManager.CreateAsync(new IdentityRole("FarmOwner"));

        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Owner", IsActive = true, EmailConfirmed = true };
        await userManager.CreateAsync(user, "P@ssword123");
        await userManager.AddToRoleAsync(user, "FarmOwner");

        var farm = new Farm
        {
            Name = "Owner Farm",
            OwnerUserId = user.Id,
            IsApproved = true,
            ApprovedAtUtc = DateTime.UtcNow
        };
        db.Farms.Add(farm);
        await db.SaveChangesAsync();

        if (!await db.TaskTypes.AnyAsync(item => item.Id == 1))
        {
            db.TaskTypes.Add(new Greenmind.Domain.Entities.TaskType
            {
                Id = 1,
                Name = "Irrigation",
                Description = "Watering schedule and management"
            });
            await db.SaveChangesAsync();
        }

        var export = new ExportPlan { Name = "Export1", FarmId = farm.Id, CropId = 1, DestinationId = 1, Governorate = "G", City = "C", Latitude = 0, Longitude = 0, AreaInFeddan = 1, PlantingDate = DateOnly.FromDateTime(DateTime.UtcNow), ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow), ExpectedExportDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress };
        db.ExportPlans.Add(export);
        await db.SaveChangesAsync();

        // add tasks
        var task = new FarmTask { ExportPlanId = export.Id, TaskTypeId = 1, Title = "T1", PlannedDate = DateOnly.FromDateTime(DateTime.UtcNow), Status = Greenmind.Domain.Consts.Enums.TaskStatus.Pending, Priority = Greenmind.Domain.Consts.Enums.TaskPriority.Medium, CreatedAt = DateTime.UtcNow };
        db.FarmTasks.Add(task);
        await db.SaveChangesAsync();

        // login to get token
        var client = _factory.CreateClient();
        var login = new LoginRequest { Email = email, Password = "P@ssword123" };
        var res = await client.PostAsJsonAsync("/api/auth/login", login);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        return (body!.Data!.Token, task.Id);
    }

    [Fact]
    public async Task Owner_CanList_And_Update_Task()
    {
        var owner = await CreateAndLoginOwnerAsync("owner1@test.local");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);

        var listRes = await client.GetAsync("/api/tasks");
        listRes.EnsureSuccessStatusCode();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var response = await listRes.Content.ReadFromJsonAsync<ApiResponse<List<TaskDto>>>(jsonOptions);
        var items = response?.Data;
        Assert.NotNull(items);
        Assert.NotEmpty(items);

        var task = items!.First();
        var patch = new UpdateTaskRequest { Status = "Completed", ActualDate = DateOnly.FromDateTime(DateTime.UtcNow), Notes = "Done" };
        var patchRes = await client.PatchAsJsonAsync($"/api/tasks/{task.Id}", patch);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, patchRes.StatusCode);
    }

    [Fact]
    public async Task Owner_Cannot_Update_Task_NotOwned()
    {
        // create owner A and task
        var ownerA = await CreateAndLoginOwnerAsync("ownerA@test.local");

        // create owner B and login
        var ownerB = await CreateAndLoginOwnerAsync("ownerB@test.local");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerB.Token);

        // Owner B tries to update Owner A's task.
        var patch = new UpdateTaskRequest { Status = "Completed", ActualDate = DateOnly.FromDateTime(DateTime.UtcNow), Notes = "Done" };
        var patchRes = await client.PatchAsJsonAsync($"/api/tasks/{ownerA.TaskId}", patch);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, patchRes.StatusCode);
    }
}
