using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Greenmind.Application.Services;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Agents.Prompts;
using Greenmind.Infrastructure.Persistence;
using Greenmind.Infrastructure.Planning;
using Microsoft.EntityFrameworkCore;

namespace Greenmind.Application.Tests.Planning;

public sealed class ExportPlanPlanningServiceTests
{
    [Fact]
    public async Task RenamedStrawberry_RetainsFifteenDayPreHarvestInterval()
    {
        var today = new DateOnly(2026, 8, 9);
        var options = new DbContextOptionsBuilder<GreenmindDbContext>()
            .UseInMemoryDatabase($"planning-phi-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new GreenmindDbContext(options);

        var crop = new Crop
        {
            Id = CropCatalog.Strawberry.Id,
            Name = "Renamed premium berries"
        };
        var destination = new Destination { Id = 1, Name = "Germany" };
        var farm = new Farm
        {
            Id = 1,
            Name = "Regression farm",
            OwnerUserId = "planning-owner",
            IsApproved = true
        };
        dbContext.AddRange(
            crop,
            destination,
            farm,
            new TaskType { Id = 1, Name = "Irrigation" },
            new TaskType { Id = 3, Name = "PestControl" });
        await dbContext.SaveChangesAsync();

        var plan = new ExportPlan
        {
            FarmId = farm.Id,
            CropId = crop.Id,
            DestinationId = destination.Id,
            Name = "Renamed strawberry plan",
            Governorate = "Giza",
            City = "Giza",
            Latitude = 30,
            Longitude = 31,
            AreaInFeddan = 5,
            PlantingDate = today.AddDays(-30),
            ExpectedHarvestDate = today.AddDays(12),
            ExpectedExportDate = today.AddDays(20),
            Status = ExportPlanStatus.InProgress
        };
        dbContext.ExportPlans.Add(plan);
        await dbContext.SaveChangesAsync();

        var agent = new PlanningAgentStub(new AgentResponse
        {
            Tasks =
            [
                CreateTask("PestControl", today.AddDays(2)),
                CreateTask("Irrigation", today.AddDays(1))
            ]
        });
        var service = new ExportPlanPlanningService(
            dbContext,
            agent,
            new TaskPersistenceStub(),
            new WeatherStub(),
            new SoilStub(),
            new SeasonCalculationService(),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 9, 8, 0, 0, TimeSpan.Zero)));

        var generated = await service.GenerateRevisionTasksAsync(plan.Id);

        Assert.DoesNotContain(generated, item => item.TaskTypeName == "PestControl");
        Assert.Contains(generated, item => item.TaskTypeName == "Irrigation");
        Assert.Equal(CropCatalog.Strawberry.Id, agent.Request?.ExportPlan.CropId);
        var prompt = new FarmPlanningPromptBuilder().BuildSystemPrompt(agent.Request!);
        Assert.Contains("minimum of 15 days", prompt, StringComparison.Ordinal);
    }

    private static TaskItemDto CreateTask(string taskTypeName, DateOnly plannedDate) => new()
    {
        TaskTypeName = taskTypeName,
        Title = "مهمة زراعية",
        Description = "وصف المهمة الزراعية",
        Quantity = 1,
        Unit = "زيارة",
        PlannedDate = plannedDate,
        Priority = TaskPriority.Medium
    };

    private sealed class PlanningAgentStub(AgentResponse response) : IAgentPlanningService
    {
        public AgentRequest? Request { get; private set; }

        public Task<AgentResponse> GeneratePlanAsync(AgentRequest request)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }

    private sealed class TaskPersistenceStub : IFarmTaskPersistenceService
    {
        public Task SaveGeneratedTasksAsync(int exportPlanId, AgentResponse agentResponse) =>
            Task.CompletedTask;
    }

    private sealed class WeatherStub : IWeatherHttpClient
    {
        public Task<WeatherForecastDto> GetForecastAsync(
            double latitude,
            double longitude,
            DateOnly start,
            DateOnly end) => Task.FromResult(new WeatherForecastDto());
    }

    private sealed class SoilStub : ISoilMeasurementRepository
    {
        public Task<SoilMeasurementDto?> GetLatestAsync(int exportPlanId) =>
            Task.FromResult<SoilMeasurementDto?>(null);

        public Task<List<SoilMeasurementDto>> GetHistoryAsync(int exportPlanId, DateOnly since) =>
            Task.FromResult(new List<SoilMeasurementDto>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
