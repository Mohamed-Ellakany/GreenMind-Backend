using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Api.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Disease;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Application.Dtos.Disease;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class DiseaseAndAttachmentApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task DiseaseReport_ValidatesOwnership_StoresPrivately_AndCompletesAsync()
    {
        var planId = await SeedPlanAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var otherOwnerClient = CreateOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StringContent(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")),
            "ObservationDate");
        multipart.Add(new StringContent("الأوراق السفلية"), "AffectedArea");
        var imageContent = new ByteArrayContent(ValidPngBytes());
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(imageContent, "Images", "leaf.png");

        var submitResponse = await ownerClient.PostAsync(
            $"/api/export-plans/{planId}/disease-reports",
            multipart);
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);
        var submitted = await submitResponse.Content
            .ReadFromJsonAsync<ApiResponse<DiseaseReportDto>>(JsonOptions);
        Assert.NotNull(submitted?.Data);
        Assert.Equal(DiseaseAnalysisStatus.Waiting, submitted!.Data!.Status);

        var crossTenantResponse = await otherOwnerClient.GetAsync(
            $"/api/export-plans/{planId}/disease-reports");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IDiseaseReportService>();
            Assert.True(await service.ProcessNextPendingAsync());
        }

        var detailResponse = await ownerClient.GetAsync(
            $"/api/export-plans/{planId}/disease-reports/{submitted.Data.Id}");
        var detail = await detailResponse.Content
            .ReadFromJsonAsync<ApiResponse<DiseaseReportDto>>(JsonOptions);
        Assert.Equal(DiseaseAnalysisStatus.Completed, detail?.Data?.Status);
        Assert.Equal(55m, detail?.Data?.ConfidencePercentage);
        Assert.Contains("متخصص", detail?.Data?.AdvisoryWarning);

        using var verifyScope = factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var key = await dbContext.DiseaseImages
            .Where(item => item.DiseaseReportId == submitted.Data.Id)
            .Select(item => item.StorageKey)
            .SingleAsync();
        Assert.DoesNotContain("leaf.png", key, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(
            factory.PrivateStorageRoot,
            key.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task DiseaseReport_RejectsSpoofedImageSignature()
    {
        var planId = await SeedPlanAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StringContent(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")),
            "ObservationDate");
        var fakeImage = new ByteArrayContent("not an image"u8.ToArray());
        fakeImage.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(fakeImage, "Images", "fake.png");

        var response = await ownerClient.PostAsync(
            $"/api/export-plans/{planId}/disease-reports",
            multipart);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponse<DiseaseReportDto>>(JsonOptions);
        Assert.Equal(ApiErrorCodes.DiseaseImageInvalid, body?.Error?.Code);
    }

    [Fact]
    public async Task ExpenseAttachment_IsPrivate_AndBecomesInaccessibleAfterExpenseDeletion()
    {
        var seeded = await SeedExpenseAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var otherOwnerClient = CreateOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StringContent(ExpenseAttachmentType.Receipt.ToArabic()),
            "AttachmentType");
        var pdfBytes = "%PDF-1.7\nprivate receipt"u8.ToArray();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(fileContent, "File", "receipt.pdf");

        var uploadResponse = await ownerClient.PostAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{seeded.ExpenseId}/attachments",
            multipart);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseAttachmentDto>>(JsonOptions);
        Assert.NotNull(uploaded?.Data);

        var crossTenantDownload = await otherOwnerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{seeded.ExpenseId}/attachments/{uploaded!.Data!.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantDownload.StatusCode);

        var download = await ownerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{seeded.ExpenseId}/attachments/{uploaded.Data.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(pdfBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.True(download.Headers.CacheControl?.Private);

        var deleteExpense = await ownerClient.DeleteAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{seeded.ExpenseId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteExpense.StatusCode);
        var afterDelete = await ownerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{seeded.ExpenseId}/attachments/{uploaded.Data.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    private async Task<int> SeedPlanAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farmId = await dbContext.Farms
            .Where(item => item.OwnerUserId == GreenmindApiFactory.FarmOwnerUserId)
            .Select(item => item.Id)
            .SingleAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var plan = new ExportPlan
        {
            FarmId = farmId,
            CropId = 1,
            DestinationId = 1,
            Name = $"Disease plan {suffix}",
            Governorate = "القاهرة",
            City = "القاهرة",
            AreaInFeddan = 5,
            PlantingDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            ExpectedHarvestDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            ExpectedExportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            Status = ExportPlanStatus.Planning
        };
        dbContext.ExportPlans.Add(plan);
        await dbContext.SaveChangesAsync();
        return plan.Id;
    }

    private async Task<(int PlanId, int ExpenseId)> SeedExpenseAsync()
    {
        var planId = await SeedPlanAsync();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var plan = await dbContext.ExportPlans.Include(item => item.Farm)
            .SingleAsync(item => item.Id == planId);
        var category = await dbContext.ExpenseCategories
            .FirstOrDefaultAsync(item => item.FarmId == plan.FarmId);
        if (category is null)
        {
            category = new ExpenseCategory
            {
                FarmId = plan.FarmId,
                Name = $"مرفقات {Guid.NewGuid():N}",
                IsDefault = false
            };
            dbContext.ExpenseCategories.Add(category);
        }
        var expense = new Expense
        {
            ExportPlanId = planId,
            ExpenseCategory = category,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = "مصروف بمرفق",
            Quantity = 1,
            UnitPrice = 100,
            PaymentMethod = ExpensePaymentMethod.Cash,
            CreatedByUserId = GreenmindApiFactory.FarmOwnerUserId
        };
        expense.RecalculateAmount();
        dbContext.Expenses.Add(expense);
        await dbContext.SaveChangesAsync();
        return (planId, expense.Id);
    }

    private HttpClient CreateOwnerClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.FarmOwner);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static byte[] ValidPngBytes() =>
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
