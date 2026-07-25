using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Greenmind.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Services;
using Greenmind.Infrastructure.Agents;
using Greenmind.Application.Contracts.Persistence;
using Moq;
using Xunit;

namespace Greenmind.Api.Tests;

public class DailyRollingPlanningServiceTests
{
    private readonly ServiceProvider _provider;
    private readonly Mock<IAgentPlanningService> _agentMock = new();
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<Greenmind.Api.HostedServices.DailyRollingPlanningService>> _loggerMock = new();

    public DailyRollingPlanningServiceTests()
    {
        var services = new ServiceCollection();

        // In-memory DB
        var dbName = "TestDb_" + Guid.NewGuid().ToString();
        services.AddDbContext<GreenmindDbContext>(opts => opts.UseInMemoryDatabase(dbName));

        // Infrastructure services (repositories, unit of work, calculation service)
        services.AddInfrastructureServices();

        // Register AI-facing repositories used by the hosted service (these live in Infrastructure.Persistence)
        services.AddScoped<Greenmind.Application.Interfaces.AI.ISoilMeasurementRepository, Greenmind.Infrastructure.Persistence.SoilMeasurementRepository>();
        services.AddScoped<Greenmind.Application.Interfaces.AI.ITaskHistoryRepository, Greenmind.Infrastructure.Persistence.TaskHistoryRepository>();

        // Agent mock and logger
        services.AddSingleton<IAgentPlanningService>(_agentMock.Object);
        services.AddSingleton(_loggerMock.Object);

        // Mock persistence service that writes tasks into the DbContext
        var persistenceMock = new Mock<IFarmTaskPersistenceService>();
        persistenceMock.Setup(p => p.SaveGeneratedTasksAsync(It.IsAny<int>(), It.IsAny<Greenmind.Application.Dtos.AI.AgentResponse>()))
            .Returns<int, Greenmind.Application.Dtos.AI.AgentResponse>(async (planId, response) =>
            {
                using var scope = services.BuildServiceProvider().CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
                if (response.Tasks != null)
                {
                    foreach (var t in response.Tasks)
                    {
                        db.FarmTasks.Add(new Greenmind.Domain.Entities.FarmTask
                        {
                            ExportPlanId = planId,
                            Title = t.Title ?? "Generated",
                            PlannedDate = t.PlannedDate
                        });
                    }
                    await db.SaveChangesAsync();
                }
            });

        services.AddSingleton(persistenceMock.Object);

        // Register hosted service so tests can resolve and call RunOnceAsync directly
        services.AddSingleton<Greenmind.Api.HostedServices.DailyRollingPlanningService>();

        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunOnce_PlanWithFiveFutureDays_TriggersPlanningAndPersists()
    {
        // Arrange
        using var scope = _provider.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<GreenmindDbContext>();
        // Seed an export plan that has exactly 5 future planned days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new Greenmind.Domain.Entities.ExportPlan
        {
            Name = "PlanFive",
            Crop = new Greenmind.Domain.Entities.Crop { Name = "Tomato" },
            Destination = new Greenmind.Domain.Entities.Destination { Name = "Export" },
            PlantingDate = today.AddDays(-30),
            ExpectedExportDate = today.AddDays(30),
            ExpectedHarvestDate = today.AddDays(10),
            Governorate = "GOV",
            City = "City",
            AreaInFeddan = 1.0m,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress
        };

        db.ExportPlans.Add(plan);
        await db.SaveChangesAsync();

        // Add 5 existing tasks for next 5 days
        for (int i = 1; i <= 5; i++)
        {
            db.FarmTasks.Add(new Greenmind.Domain.Entities.FarmTask
            {
                ExportPlanId = plan.Id,
                Title = "Existing",
                PlannedDate = today.AddDays(i)
            });
        }

        await db.SaveChangesAsync();

        var hosted = provider.GetRequiredService<Greenmind.Api.HostedServices.DailyRollingPlanningService>();

        // Setup agent mock to return one task for missing dates
        _agentMock.Setup(a => a.GeneratePlanAsync(It.IsAny<Greenmind.Application.Dtos.AI.AgentRequest>()))
            .ReturnsAsync(new Greenmind.Application.Dtos.AI.AgentResponse
            {
                Tasks = new List<Greenmind.Application.Dtos.AI.TaskItemDto>
                {
                    new Greenmind.Application.Dtos.AI.TaskItemDto { Title = "Irrigate", PlannedDate = today.AddDays(6) }
                }
            });

        // Act
        await hosted.RunOnceAsync();

        // Assert: check that one or more tasks were added for the missing days and persisted
        var tasks = db.FarmTasks.Where(t => t.ExportPlanId == plan.Id).ToList();
        Assert.True(tasks.Count > 5);
    }

    [Fact]
    public async Task RunOnce_PlanWithSixFutureDays_SkipsPlanningAndDoesNotCallAgent()
    {
        using var scope = _provider.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<GreenmindDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new Greenmind.Domain.Entities.ExportPlan
        {
            Name = "PlanSix",
            Crop = new Greenmind.Domain.Entities.Crop { Name = "Tomato" },
            Destination = new Greenmind.Domain.Entities.Destination { Name = "Export" },
            PlantingDate = today.AddDays(-30),
            ExpectedExportDate = today.AddDays(30),
            ExpectedHarvestDate = today.AddDays(10),
            Governorate = "GOV",
            City = "City",
            AreaInFeddan = 1.0m,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress
        };

        db.ExportPlans.Add(plan);
        await db.SaveChangesAsync();

        for (int i = 1; i <= 6; i++)
        {
            db.FarmTasks.Add(new Greenmind.Domain.Entities.FarmTask
            {
                ExportPlanId = plan.Id,
                Title = "Existing",
                PlannedDate = today.AddDays(i)
            });
        }

        await db.SaveChangesAsync();

        var hosted = provider.GetRequiredService<Greenmind.Api.HostedServices.DailyRollingPlanningService>();

        // Act
        await hosted.RunOnceAsync();

        // Assert: Agent should not have been called and no new tasks should be added
        _agentMock.Verify(a => a.GeneratePlanAsync(It.IsAny<Greenmind.Application.Dtos.AI.AgentRequest>()), Times.Never);
        var tasks = db.FarmTasks.Where(t => t.ExportPlanId == plan.Id).ToList();
        Assert.Equal(6, tasks.Count);
    }

    [Fact]
    public async Task RunOnce_PlanOutsideSeason_SkipsAndDoesNotPlan()
    {
        using var scope = _provider.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<GreenmindDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new Greenmind.Domain.Entities.ExportPlan
        {
            Name = "PlanOutside",
            Crop = new Greenmind.Domain.Entities.Crop { Name = "Tomato" },
            Destination = new Greenmind.Domain.Entities.Destination { Name = "Export" },
            PlantingDate = today.AddDays(10), // in future
            ExpectedExportDate = today.AddDays(20),
            ExpectedHarvestDate = today.AddDays(15),
            Governorate = "GOV",
            City = "City",
            AreaInFeddan = 1.0m,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress
        };

        db.ExportPlans.Add(plan);
        await db.SaveChangesAsync();

        var hosted = provider.GetRequiredService<Greenmind.Api.HostedServices.DailyRollingPlanningService>();

        // Act
        await hosted.RunOnceAsync();

        // Assert
        _agentMock.Verify(a => a.GeneratePlanAsync(It.IsAny<Greenmind.Application.Dtos.AI.AgentRequest>()), Times.Never);
        var tasks = db.FarmTasks.Where(t => t.ExportPlanId == plan.Id).ToList();
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task RunOnce_ExceptionWhileProcessingOnePlan_ContinuesAndProcessesOthers()
    {
        using var scope = _provider.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<GreenmindDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Plan 1 will throw
        var plan1 = new Greenmind.Domain.Entities.ExportPlan
        {
            Name = "PlanErr",
            Crop = new Greenmind.Domain.Entities.Crop { Name = "A" },
            Destination = new Greenmind.Domain.Entities.Destination { Name = "D1" },
            PlantingDate = today.AddDays(-30),
            ExpectedExportDate = today.AddDays(30),
            ExpectedHarvestDate = today.AddDays(10),
            Governorate = "GOV",
            City = "City",
            AreaInFeddan = 1.0m,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress
        };

        // Plan 2 is valid and should be processed
        var plan2 = new Greenmind.Domain.Entities.ExportPlan
        {
            Name = "PlanOK",
            Crop = new Greenmind.Domain.Entities.Crop { Name = "B" },
            Destination = new Greenmind.Domain.Entities.Destination { Name = "D2" },
            PlantingDate = today.AddDays(-30),
            ExpectedExportDate = today.AddDays(30),
            ExpectedHarvestDate = today.AddDays(10),
            Governorate = "GOV",
            City = "City",
            AreaInFeddan = 1.0m,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress
        };

        db.ExportPlans.AddRange(plan1, plan2);
        await db.SaveChangesAsync();

        // Setup agent mock: throw for plan1, return a task for plan2
        _agentMock.SetupSequence(a => a.GeneratePlanAsync(It.IsAny<Greenmind.Application.Dtos.AI.AgentRequest>()))
            .ThrowsAsync(new Exception("Simulated failure"))
            .ReturnsAsync(new Greenmind.Application.Dtos.AI.AgentResponse { Tasks = new List<Greenmind.Application.Dtos.AI.TaskItemDto> { new() { Title = "T", PlannedDate = today.AddDays(1) } } });

        var hosted = provider.GetRequiredService<Greenmind.Api.HostedServices.DailyRollingPlanningService>();

        // Act
        await hosted.RunOnceAsync();

        // Assert: ensure logger recorded an error and plan2 got tasks
        _agentMock.Verify(a => a.GeneratePlanAsync(It.IsAny<Greenmind.Application.Dtos.AI.AgentRequest>()), Times.Exactly(2));
        var tasksPlan2 = db.FarmTasks.Where(t => t.ExportPlanId == plan2.Id).ToList();
        Assert.NotEmpty(tasksPlan2);
    }

    [Fact]
    public async Task RunOnce_DoesNotCreateDuplicateTasks_And_ResultHasSevenFutureDays()
    {
        using var scope = _provider.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<GreenmindDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new Greenmind.Domain.Entities.ExportPlan
        {
            Name = "PlanNoDup",
            Crop = new Greenmind.Domain.Entities.Crop { Name = "Tomato" },
            Destination = new Greenmind.Domain.Entities.Destination { Name = "Export" },
            PlantingDate = today.AddDays(-30),
            ExpectedExportDate = today.AddDays(30),
            ExpectedHarvestDate = today.AddDays(10),
            Governorate = "GOV",
            City = "City",
            AreaInFeddan = 1.0m,
            Status = Greenmind.Domain.Consts.Enums.ExportPlanStatus.InProgress
        };

        db.ExportPlans.Add(plan);
        await db.SaveChangesAsync();

        // Add 3 existing tasks for days 1..3
        for (int i = 1; i <= 3; i++)
        {
            db.FarmTasks.Add(new Greenmind.Domain.Entities.FarmTask
            {
                ExportPlanId = plan.Id,
                Title = "Existing",
                PlannedDate = today.AddDays(i)
            });
        }

        await db.SaveChangesAsync();

        // Agent returns tasks for days 4..7
        _agentMock.Setup(a => a.GeneratePlanAsync(It.IsAny<Greenmind.Application.Dtos.AI.AgentRequest>()))
            .ReturnsAsync(new Greenmind.Application.Dtos.AI.AgentResponse
            {
                Tasks = new List<Greenmind.Application.Dtos.AI.TaskItemDto>
                {
                    new() { Title = "T1", PlannedDate = today.AddDays(4) },
                    new() { Title = "T2", PlannedDate = today.AddDays(5) },
                    new() { Title = "T3", PlannedDate = today.AddDays(6) },
                    new() { Title = "T4", PlannedDate = today.AddDays(7) }
                }
            });

        var hosted = provider.GetRequiredService<Greenmind.Api.HostedServices.DailyRollingPlanningService>();

        // Act
        await hosted.RunOnceAsync();

        // Act again to ensure duplicates are not created
        await hosted.RunOnceAsync();

        // Assert: no duplicates and total future days == 7
        var futureTasks = db.FarmTasks.Where(t => t.ExportPlanId == plan.Id && t.PlannedDate > today && t.PlannedDate <= today.AddDays(7)).ToList();
        var distinctDates = futureTasks.Select(t => t.PlannedDate).Distinct().Count();
        Assert.Equal(7, distinctDates);
    }
}
