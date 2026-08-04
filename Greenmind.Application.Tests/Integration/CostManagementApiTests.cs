using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Greenmind.Api.Json;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Dtos.Costs;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class CostManagementApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Expense_WorksWithoutBudget_CalculatesAmount_AndSoftDeletes()
    {
        var seeded = await SeedPlanAndCategoryAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var noBudgetResponse = await ownerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/budget");
        Assert.Equal(HttpStatusCode.OK, noBudgetResponse.StatusCode);
        var noBudget = await noBudgetResponse.Content
            .ReadFromJsonAsync<ApiResponse<BudgetDto?>>(JsonOptions);
        Assert.Null(noBudget?.Data);

        var request = BuildExpenseRequest(seeded.CategoryId);
        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            request,
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Assert.Equal(1500.00m, created!.Data!.Amount);
        Assert.Equal("EGP", created.Data.Currency);
        Assert.Equal(ExpensePaymentMethod.BankTransfer, created.Data.PaymentMethod);

        var deleteResponse = await ownerClient.DeleteAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await ownerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses");
        var list = await listResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpensePageDto>>(JsonOptions);
        Assert.Equal(0, list?.Data?.TotalCount);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var stored = await dbContext.Expenses
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == created.Data.Id);
        Assert.True(stored.IsDeleted);
        Assert.Equal(GreenmindApiFactory.FarmOwnerUserId, stored.DeletedByUserId);
        Assert.NotNull(stored.DeletedAtUtc);
    }

    [Fact]
    public async Task Budget_CanBeAddedAfterExpenses_AndValidatesLineTotal()
    {
        var seeded = await SeedPlanAndCategoryAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            BuildExpenseRequest(seeded.CategoryId),
            JsonOptions);

        var invalidResponse = await ownerClient.PutAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/budget",
            new BudgetRequest
            {
                TotalBudget = 1000m,
                Lines =
                [
                    new BudgetLineRequest
                    {
                        ExpenseCategoryId = seeded.CategoryId,
                        PlannedAmount = 1500m
                    }
                ]
            },
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var invalid = await invalidResponse.Content
            .ReadFromJsonAsync<ApiResponse<BudgetDto>>(JsonOptions);
        Assert.Equal(ApiErrorCodes.BudgetLinesExceedTotal, invalid?.Error?.Code);

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/budget",
            new BudgetRequest
            {
                TotalBudget = 5000m,
                Notes = "ميزانية الموسم",
                Lines =
                [
                    new BudgetLineRequest
                    {
                        ExpenseCategoryId = seeded.CategoryId,
                        PlannedAmount = 3000m
                    }
                ]
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var budget = await response.Content
            .ReadFromJsonAsync<ApiResponse<BudgetDto>>(JsonOptions);
        Assert.Equal(5000m, budget?.Data?.EffectivePlannedBudget);
        Assert.Single(budget!.Data!.Lines);
    }

    [Fact]
    public async Task Expense_RejectsAnotherFarmsCategoryAndPlan()
    {
        var owned = await SeedPlanAndCategoryAsync();
        var other = await SeedPlanAndCategoryAsync(
            GreenmindApiFactory.OtherFarmOwnerUserId);
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var otherOwnerClient = CreateOwnerClient(GreenmindApiFactory.OtherFarmOwnerUserId);

        var wrongCategoryResponse = await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{owned.PlanId}/expenses",
            BuildExpenseRequest(other.CategoryId),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCategoryResponse.StatusCode);
        var wrongCategory = await wrongCategoryResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);
        Assert.Equal(ApiErrorCodes.ExpenseCategoryInvalid, wrongCategory?.Error?.Code);

        var hiddenPlanResponse = await otherOwnerClient.PostAsJsonAsync(
            $"/api/export-plans/{owned.PlanId}/expenses",
            BuildExpenseRequest(owned.CategoryId),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, hiddenPlanResponse.StatusCode);
    }

    [Fact]
    public async Task Expense_CanBeUpdatedWithoutChangingItsCategory()
    {
        var seeded = await SeedPlanAndCategoryAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            BuildExpenseRequest(seeded.CategoryId),
            JsonOptions);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);

        var updatedRequest = BuildExpenseRequest(seeded.CategoryId);
        updatedRequest = new ExpenseRequest
        {
            ExpenseCategoryId = seeded.CategoryId,
            ExpenseDate = updatedRequest.ExpenseDate,
            Description = "أسمدة محدثة",
            Supplier = "المورد الجديد",
            Quantity = 5m,
            UnitPrice = 10m,
            PaymentMethod = ExpensePaymentMethod.Cash,
            Notes = "تمت المراجعة"
        };

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{created!.Data!.Id}",
            updatedRequest,
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);
        Assert.Equal("أسمدة محدثة", updated?.Data?.Description);
        Assert.Equal(50m, updated?.Data?.Amount);
        Assert.Equal(seeded.CategoryId, updated?.Data?.ExpenseCategory.Id);
    }

    [Fact]
    public async Task Expense_CanBeUpdatedAndMovedToAnotherOwnedCategory()
    {
        var seeded = await SeedPlanAndCategoryAsync();
        int replacementCategoryId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
            var farmId = await dbContext.ExportPlans
                .Where(item => item.Id == seeded.PlanId)
                .Select(item => item.FarmId)
                .SingleAsync();
            var replacementCategory = new ExpenseCategory
            {
                FarmId = farmId,
                Name = $"Replacement category {Guid.NewGuid():N}",
                IsDefault = false
            };
            dbContext.ExpenseCategories.Add(replacementCategory);
            await dbContext.SaveChangesAsync();
            replacementCategoryId = replacementCategory.Id;
        }

        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var createResponse = await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            BuildExpenseRequest(seeded.CategoryId),
            JsonOptions);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);

        var updateRequest = BuildExpenseRequest(replacementCategoryId);
        updateRequest = new ExpenseRequest
        {
            ExpenseCategoryId = replacementCategoryId,
            ExpenseDate = updateRequest.ExpenseDate,
            Description = updateRequest.Description,
            Supplier = updateRequest.Supplier,
            Quantity = 0.5m,
            UnitPrice = 40m,
            PaymentMethod = updateRequest.PaymentMethod,
            Notes = updateRequest.Notes
        };

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{created!.Data!.Id}",
            updateRequest,
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);
        Assert.Equal(replacementCategoryId, updated?.Data?.ExpenseCategory.Id);
        Assert.Equal(0.5m, updated?.Data?.Quantity);
        Assert.Equal(20m, updated?.Data?.Amount);
    }

    [Fact]
    public async Task DashboardAndCsv_ExcludeDeletedExpenses_AndEscapeFormulas()
    {
        var seeded = await SeedPlanAndCategoryAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);

        var deletedResponse = await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            BuildExpenseRequest(seeded.CategoryId),
            JsonOptions);
        var deleted = await deletedResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExpenseDto>>(JsonOptions);
        await ownerClient.DeleteAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/{deleted!.Data!.Id}");

        var activeRequest = BuildExpenseRequest(seeded.CategoryId);
        activeRequest = new ExpenseRequest
        {
            ExpenseCategoryId = activeRequest.ExpenseCategoryId,
            ExpenseDate = activeRequest.ExpenseDate,
            Description = "=1+1",
            Supplier = activeRequest.Supplier,
            Quantity = activeRequest.Quantity,
            UnitPrice = activeRequest.UnitPrice,
            PaymentMethod = activeRequest.PaymentMethod,
            Notes = activeRequest.Notes
        };
        await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            activeRequest,
            JsonOptions);
        await ownerClient.PutAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/budget",
            new BudgetRequest { TotalBudget = 2000m },
            JsonOptions);

        var dashboardResponse = await ownerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/cost-dashboard");
        var dashboard = await dashboardResponse.Content
            .ReadFromJsonAsync<ApiResponse<CostDashboardDto>>(JsonOptions);
        Assert.Equal(1500m, dashboard?.Data?.TotalExpenses);
        Assert.Equal(500m, dashboard?.Data?.RemainingBudget);
        Assert.Equal(75m, dashboard?.Data?.BudgetConsumptionPercentage);
        Assert.Equal(300m, dashboard?.Data?.CostPerFeddan);
        Assert.Single(dashboard!.Data!.LatestExpenses);

        var csvResponse = await ownerClient.GetAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses/export.csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("التاريخ,الفئة,الوصف", csv);
        Assert.Contains("'=1+1", csv);
        Assert.DoesNotContain("شراء مستلزمات زراعية", csv);
    }

    [Fact]
    public async Task FinancialPdfReport_ExportsForOwnedPlanAndSelectedCategory()
    {
        var seeded = await SeedPlanAndCategoryAsync();
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        await ownerClient.PostAsJsonAsync(
            $"/api/export-plans/{seeded.PlanId}/expenses",
            BuildExpenseRequest(seeded.CategoryId),
            JsonOptions);
        for (var index = 1; index <= 18; index++)
        {
            var createResponse = await ownerClient.PostAsJsonAsync(
                $"/api/export-plans/{seeded.PlanId}/expenses",
                new ExpenseRequest
                {
                    ExpenseCategoryId = seeded.CategoryId,
                    ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-index)),
                    Description = $"مصروف تشغيل زراعي رقم {index} مع وصف تفصيلي للتحقق من التفاف النص داخل الجدول",
                    Supplier = $"المورد الزراعي رقم {index}",
                    Quantity = index + 1,
                    UnitPrice = 75.50m,
                    PaymentMethod = (ExpensePaymentMethod)(index % 4 + 1),
                    Notes = "بيانات اختبار تقرير المصروفات متعدد الصفحات"
                },
                JsonOptions);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        }

        var response = await ownerClient.GetAsync(
            $"/api/farm-owner/reports/export?format=pdf&sections=spending&exportPlanId={seeded.PlanId}&expenseCategoryId={seeded.CategoryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 20_000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.Contains("%%EOF", System.Text.Encoding.ASCII.GetString(bytes[^64..]));
        Assert.Contains("greenmind-expenses-report-", response.Content.Headers.ContentDisposition?.ToString());

    }

    [Fact]
    public async Task FinancialPdfReport_RejectsAnInvertedDateRange()
    {
        var ownerClient = CreateOwnerClient(GreenmindApiFactory.FarmOwnerUserId);
        var response = await ownerClient.GetAsync(
            "/api/farm-owner/reports/export?format=pdf&sections=spending&from=2026-08-02&to=2026-08-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(int PlanId, int CategoryId)> SeedPlanAndCategoryAsync(
        string ownerUserId = GreenmindApiFactory.FarmOwnerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var farmId = await dbContext.Farms
            .Where(farm => farm.OwnerUserId == ownerUserId)
            .Select(farm => farm.Id)
            .SingleAsync();
        var category = await dbContext.ExpenseCategories
            .FirstOrDefaultAsync(item => item.FarmId == farmId);
        if (category is null)
        {
            category = new ExpenseCategory
            {
                FarmId = farmId,
                Name = $"اختبار {Guid.NewGuid():N}",
                IsDefault = false
            };
            dbContext.ExpenseCategories.Add(category);
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var plan = new ExportPlan
        {
            FarmId = farmId,
            CropId = 1,
            DestinationId = 1,
            Name = $"Cost plan {suffix}",
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
        return (plan.Id, category.Id);
    }

    private static ExpenseRequest BuildExpenseRequest(int categoryId) => new()
    {
        ExpenseCategoryId = categoryId,
        ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Description = "شراء مستلزمات زراعية",
        Supplier = "شركة المستلزمات",
        Quantity = 2m,
        UnitPrice = 750m,
        PaymentMethod = ExpensePaymentMethod.BankTransfer,
        Notes = "اختبار"
    };

    private HttpClient CreateOwnerClient(string userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", RoleNames.FarmOwner);
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ArabicBusinessEnumJsonConverterFactory());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
