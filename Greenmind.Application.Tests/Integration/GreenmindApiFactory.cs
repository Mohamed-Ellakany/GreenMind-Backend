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
using Greenmind.Application.Interfaces.AI;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Infrastructure.Storage;
using Greenmind.Infrastructure.Rag;
using Greenmind.Application.Contracts.ExportPlans;
using Greenmind.Application.Dtos.ExportPlans;
using Greenmind.Application.Dtos.TaskProposals;
using Greenmind.Domain.Consts;

namespace Greenmind.Application.Tests.Integration;

public sealed class GreenmindApiFactory : WebApplicationFactory<Greenmind.Api.Program>, IAsyncLifetime
{
    public const string AdminUserId = "integration-admin";
    public const string FarmOwnerUserId = "integration-farm-owner";
    public const string OtherFarmOwnerUserId = "integration-other-farm-owner";
    public const string AuthenticationScheme = "IntegrationTest";

    private readonly string _databaseName = $"greenmind-tests-{Guid.NewGuid():N}";
    public string PrivateStorageRoot { get; } = Path.Combine(
        Path.GetTempPath(),
        "greenmind-integration-tests",
        Guid.NewGuid().ToString("N"));

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
            services.Configure<PrivateFileStorageOptions>(options =>
                options.RootPath = PrivateStorageRoot);
            services.RemoveAll<IConversationAiService>();
            services.AddSingleton<IConversationAiService, StubConversationAiService>();
            services.RemoveAll<IComplianceIngestionService>();
            services.AddSingleton<IComplianceIngestionService, StubComplianceIngestionService>();
            services.RemoveAll<IPlanTaskProposalGenerator>();
            services.AddSingleton<IPlanTaskProposalGenerator, StubPlanTaskProposalGenerator>();
            services.RemoveAll<IDiseaseVisionAnalyzer>();
            services.AddSingleton<IDiseaseVisionAnalyzer, StubDiseaseVisionAnalyzer>();
            services.RemoveAll<IDiagnosisConversationAiService>();
            services.AddSingleton<IDiagnosisConversationAiService, StubDiagnosisConversationAiService>();
            services.RemoveAll<IDiagnosisKnowledgeProvider>();
            services.AddSingleton<IDiagnosisKnowledgeProvider, StubDiagnosisKnowledgeProvider>();
            services.RemoveAll<IExportScheduleAdvisor>();
            services.AddSingleton<IExportScheduleAdvisor, StubExportScheduleAdvisor>();
            services.RemoveAll<IClimateDataClient>();
            services.AddSingleton<IClimateDataClient, StubClimateDataClient>();

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
        if (Directory.Exists(PrivateStorageRoot))
            Directory.Delete(PrivateStorageRoot, recursive: true);
        return Task.CompletedTask;
    }
}

internal sealed class StubExportScheduleAdvisor : IExportScheduleAdvisor
{
    public Task<ExportScheduleAdvisorResult> AnalyzeAsync(
        ExportScheduleAdvisorContext context,
        CancellationToken cancellationToken = default)
    {
        var planting = context.RequestedPlantingDate ?? context.Today.AddDays(10);
        var harvest = context.RequestedHarvestDate ?? planting.AddDays(90);
        var exporting = context.RequestedExportDate ?? harvest.AddDays(7);
        return Task.FromResult(new ExportScheduleAdvisorResult(
            "موسم اختباري",
            planting,
            harvest,
            exporting,
            "Feasible",
            "High",
            "تحليل اختباري حسب الإحداثيات.",
            ["تم استخدام الإحداثيات"],
            [],
            [],
            [],
            [new ScheduleAnalysisSourceDto(
                "test-source", "Test climate", "Integration tests", null,
                DateTime.UtcNow, "OfficialSource", ["climate"])],
            []));
    }
}

internal sealed class StubDiseaseVisionAnalyzer : IDiseaseVisionAnalyzer
{
    public Task<DiseaseVisionResult> AnalyzeAsync(
        DiseaseVisionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DiseaseVisionResult(
            "اشتباه إصابة فطرية",
            55m,
            DiseaseSeverity.Medium,
            "بقع ظاهرة على الأوراق",
            "اعزل الأجزاء المتأثرة وراقب تطور الأعراض",
            "قد يتأثر توقيت التصدير إذا انتشرت الأعراض",
            "النتيجة استشارية وليست تشخيصًا معمليًا.",
            "integration-vision",
            "test-v1"));
}

internal sealed class StubDiagnosisConversationAiService : IDiagnosisConversationAiService
{
    public Task<DiagnosisConversationTurn> ReplyAsync(
        DiagnosisConversationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DiagnosisConversationTurn(
            "تم تسجيل المعلومات والصورة، وأصبحت الحالة جاهزة للتشخيص.",
            new DiagnosisFactsDto(
                "الأوراق والثمار",
                "منذ ثلاثة أيام",
                "بدأت في بقعة ثم انتشرت تدريجياً",
                "نحو 10% من النباتات",
                "رطوبة مرتفعة وري بالتنقيط",
                "لم تُستخدم معاملات سابقة")));
}

internal sealed class StubDiagnosisKnowledgeProvider : IDiagnosisKnowledgeProvider
{
    public Task<DiagnosisKnowledgeResult> SearchAsync(
        string cropCanonicalKey,
        string diseaseSearchTerm,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(cropCanonicalKey == CropCatalog.Strawberry.CanonicalKey
            ? new DiagnosisKnowledgeResult(
                [new DiagnosisKnowledgeSource(
                    "UC IPM Strawberry",
                    "https://ipm.ucanr.edu/agriculture/strawberry/",
                    "University of California Agriculture and Natural Resources",
                    "Diseases",
                    "Academic strawberry disease guidance used for the integration test.",
                    DateTime.UtcNow,
                    true)],
                null)
            : new DiagnosisKnowledgeResult([], "Unsupported crop knowledge key."));
}

internal sealed class StubPlanTaskProposalGenerator : IPlanTaskProposalGenerator
{
    public Task<IReadOnlyList<GeneratedTaskChangeItem>> TryGenerateAsync(
        string userRequest,
        string planContextJson,
        CancellationToken cancellationToken = default,
        int? exportPlanId = null)
    {
        // Diagnosis plans include this richer context. Deliberately return an
        // invalid three-item proposal so the integration test covers the
        // deterministic treatment-plan fallback after validation rejects AI output.
        if (planContextJson.Contains("\"seasonalClimate\"", StringComparison.Ordinal))
        {
            var plannedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
            IReadOnlyList<GeneratedTaskChangeItem> invalidItems =
            [
                new("add", null, new ProposedTaskValuesDto(0, "invalid", null, plannedDate, TaskPriority.High), "test"),
                new("add", null, new ProposedTaskValuesDto(0, "invalid", null, plannedDate, TaskPriority.High), "test"),
                new("add", null, new ProposedTaskValuesDto(0, "invalid", null, plannedDate, TaskPriority.High), "test")
            ];
            return Task.FromResult(invalidItems);
        }

        return Task.FromResult<IReadOnlyList<GeneratedTaskChangeItem>>([]);
    }
}

internal sealed class StubClimateDataClient : IClimateDataClient
{
    public Task<ClimateDataSnapshot> GetClimateAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ClimateDataSnapshot(
            latitude,
            longitude,
            [new ClimateMonthSummary(DateTime.UtcNow.Month, 24, 15, 31, 2, 62)],
            DateTime.UtcNow,
            "Test climate",
            "https://example.test/climate"));
}

internal sealed class StubConversationAiService : IConversationAiService
{
    public async IAsyncEnumerable<string> StreamReplyAsync(
        ConversationAiRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return "إجابة ";
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return "آمنة";
    }
}

internal sealed class StubComplianceIngestionService : IComplianceIngestionService
{
    public Task<int> IngestFfvStandardAsync(
        Stream pdfStream,
        string crop,
        string sourceName,
        CancellationToken ct = default) => Task.FromResult(7);

    public Task<int> IngestEuRegulationBodyAsync(
        Stream pdfStream,
        string sourceName,
        CancellationToken ct = default) => Task.FromResult(11);

    public Task<int> IngestEuRegulationAnnexForCropAsync(
        Stream pdfStream,
        string sourceName,
        string annexLabelHint,
        string[] cropKeywords,
        CancellationToken ct = default) => Task.FromResult(3);
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
