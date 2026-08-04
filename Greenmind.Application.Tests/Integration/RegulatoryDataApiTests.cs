using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Greenmind.Application.Contracts.Api;
using Greenmind.Application.Contracts.Regulatory;
using Greenmind.Application.Dtos.Regulatory;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Greenmind.Domain.Entities;
using Greenmind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Greenmind.Application.Tests.Integration;

public sealed class RegulatoryDataApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task FarmOwner_CannotUploadRegulatoryData()
    {
        using var client = CreateAuthenticatedClient(RoleNames.FarmOwner);
        using var content = CreateUploadContent(
            "RegionName",
            "European Union",
            CreateWorkbook([["Active Substance ID", "Substance", "Status under Reg. (EC) No 1107/2009"]]),
            "active.xlsx");

        var response = await client.PostAsync("/api/admin/regulatory-data/active-substances", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ImportsAndReplacesActiveSubstancesForSelectedRegion()
    {
        var region = await AddDestinationAsync();
        using var client = CreateAuthenticatedClient(RoleNames.Admin);

        var firstWorkbook = CreateWorkbook(
        [
            ["Active Substance ID", "Substance", "CAS Number", "Status under Reg. (EC) No 1107/2009", "Expiration of approval", "Legislation", "Remark", "Reg 396/2005", "Functions"],
            ["90001", "Substance One", "11-11-1", "Approved", "26/07/2027", "EU Law", "<p>Important</p>", "Reg 396", "Fungicide"]
        ]);

        using (var firstContent = CreateUploadContent("RegionName", region, firstWorkbook, "active.xlsx"))
        {
            var firstResponse = await client.PostAsync("/api/admin/regulatory-data/active-substances", firstContent);
            var firstResult = await ReadDataAsync<RegulatoryImportResultDto>(firstResponse);
            Assert.Equal(1, firstResult.ImportedRows);
            Assert.Equal(0, firstResult.SkippedRows);
        }

        var replacementWorkbook = CreateWorkbook(
        [
            ["Active Substance ID", "Substance", "Status under Reg. (EC) No 1107/2009"],
            ["90002", "Substance Two", "Not approved"],
            ["90002", "Substance Two", "Not approved"]
        ]);

        using (var replacementContent = CreateUploadContent("RegionName", region, replacementWorkbook, "replacement.xlsx"))
        {
            var replacementResponse = await client.PostAsync(
                "/api/admin/regulatory-data/active-substances",
                replacementContent);
            var replacementResult = await ReadDataAsync<RegulatoryImportResultDto>(replacementResponse);
            Assert.Equal(1, replacementResult.ImportedRows);
            Assert.Equal(1, replacementResult.SkippedRows);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var oldItem = await context.ActiveSubstances.SingleAsync(item => item.RegionName == region && item.ExternalId == 90001);
        var currentItem = await context.ActiveSubstances.SingleAsync(item => item.RegionName == region && item.ExternalId == 90002);

        Assert.True(oldItem.IsDeleted);
        Assert.NotNull(oldItem.DeletedAt);
        Assert.False(currentItem.IsDeleted);
        Assert.Equal(ActiveSubstanceApprovalStatus.غير_معتمد, currentItem.ApprovalStatus);

        var knowledge = scope.ServiceProvider.GetRequiredService<IRegulatoryKnowledgeQueryService>();
        Assert.Empty(await knowledge.FindActiveSubstancesAsync(region, "Substance One"));
        var currentKnowledge = Assert.Single(await knowledge.FindActiveSubstancesAsync(region, "90002"));
        Assert.Equal("غير معتمد", currentKnowledge.ApprovalStatus);
    }

    [Fact]
    public async Task Admin_ImportsMrlValuesAndSkipsExactDuplicates()
    {
        var region = await AddDestinationAsync();
        var product = await AddCropAsync();
        using var client = CreateAuthenticatedClient(RoleNames.Admin);
        var workbook = CreateWorkbook(
        [
            ["Metadata"],
            [""],
            [""],
            [""],
            [""],
            [""],
            [""],
            ["Pesticide Id", "Pesticide residue", "Maximum residue level (mg/kg)"],
            ["101", "Residue A", "0.01*"],
            ["101", "Residue A", "0.01*"],
            ["102", "Residue B", null]
        ]);

        using var content = CreateMrlUploadContent(region, product, workbook);
        var response = await client.PostAsync("/api/admin/regulatory-data/mrls", content);
        var result = await ReadDataAsync<RegulatoryImportResultDto>(response);

        Assert.Equal(2, result.ImportedRows);
        Assert.Equal(1, result.SkippedRows);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var items = await context.ProductMrls
            .Where(item => item.RegionName == region && item.ProductType == product && !item.IsDeleted)
            .OrderBy(item => item.ExternalPesticideId)
            .ToListAsync();

        Assert.Equal(2, items.Count);
        Assert.Equal(0.01m, items[0].MaximumResidueLevel);
        Assert.True(items[0].IsDetectionLimit);
        Assert.Null(items[1].MaximumResidueLevel);
        Assert.False(items[1].IsDetectionLimit);
        Assert.All(items, item => Assert.Equal("mg/kg", item.Unit));

        var knowledge = scope.ServiceProvider.GetRequiredService<IRegulatoryKnowledgeQueryService>();
        var mrlKnowledge = Assert.Single(await knowledge.FindProductMrlsAsync(region, product, "101"));
        Assert.Equal(0.01m, mrlKnowledge.MaximumResidueLevel);
        Assert.True(mrlKnowledge.IsDetectionLimit);
    }

    [Fact]
    public async Task ArabicActiveSubstanceName_FindsEnglishRegulatoryRecord()
    {
        var region = await AddDestinationAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        context.ActiveSubstances.Add(new ActiveSubstance
        {
            RegionName = region,
            ExternalId = 75,
            Name = "Propiconazole",
            CasNumber = "60207-90-1",
            ApprovalStatus = ActiveSubstanceApprovalStatus.غير_معتمد
        });
        await context.SaveChangesAsync();

        var knowledge = scope.ServiceProvider.GetRequiredService<IRegulatoryKnowledgeQueryService>();
        var result = Assert.Single(
            await knowledge.FindActiveSubstancesAsync(region, "بروبيكونازول"));

        Assert.Equal("Propiconazole", result.Name);
        Assert.Equal("60207-90-1", result.CasNumber);
        Assert.Equal("غير معتمد", result.ApprovalStatus);
    }

    [Fact]
    public async Task ActiveSubstanceMentionedInQuestion_IsReadFromRegulatoryData()
    {
        var region = await AddDestinationAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        context.ActiveSubstances.Add(new ActiveSubstance
        {
            RegionName = region,
            ExternalId = 67,
            Name = "Propanocarb",
            CasNumber = "24579-73-5",
            ApprovalStatus = ActiveSubstanceApprovalStatus.معتمد
        });
        await context.SaveChangesAsync();

        var knowledge = scope.ServiceProvider.GetRequiredService<IRegulatoryKnowledgeQueryService>();
        var result = Assert.Single(await knowledge.FindActiveSubstancesMentionedAsync(
            region,
            "هل المادة الفعالة Propanocarb معتمدة للتصدير؟"));

        Assert.Equal(67, result.ExternalId);
        Assert.Equal("24579-73-5", result.CasNumber);
        Assert.Equal("معتمد", result.ApprovalStatus);
    }

    [Fact]
    public async Task InvalidWorkbook_IsRejectedWithoutReplacingCurrentData()
    {
        var region = await AddDestinationAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        context.ActiveSubstances.Add(new ActiveSubstance
        {
            RegionName = region,
            ExternalId = 42,
            Name = "Existing",
            ApprovalStatus = ActiveSubstanceApprovalStatus.معتمد
        });
        await context.SaveChangesAsync();

        using var client = CreateAuthenticatedClient(RoleNames.Admin);
        using var content = CreateUploadContent(
            "RegionName",
            region,
            CreateWorkbook([["Wrong", "Headers"]]),
            "invalid.xlsx");

        var response = await client.PostAsync("/api/admin/regulatory-data/active-substances", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await context.ActiveSubstances.AnyAsync(item => item.RegionName == region && item.ExternalId == 42 && !item.IsDeleted));
    }

    private async Task<string> AddDestinationAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var name = $"Region-{Guid.NewGuid():N}";
        context.Destinations.Add(new Destination { Name = name });
        await context.SaveChangesAsync();
        return name;
    }

    private async Task<string> AddCropAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenmindDbContext>();
        var name = $"Product-{Guid.NewGuid():N}";
        context.Crops.Add(new Crop { Name = name });
        await context.SaveChangesAsync();
        return name;
    }

    private HttpClient CreateAuthenticatedClient(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add(
            "X-Test-UserId",
            role == RoleNames.Admin
                ? GreenmindApiFactory.AdminUserId
                : GreenmindApiFactory.FarmOwnerUserId);
        return client;
    }

    private static MultipartFormDataContent CreateUploadContent(
        string selectionName,
        string selectionValue,
        byte[] workbook,
        string fileName)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(selectionValue), selectionName);
        AddFile(content, workbook, fileName);
        return content;
    }

    private static MultipartFormDataContent CreateMrlUploadContent(
        string region,
        string product,
        byte[] workbook)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(region), "RegionName");
        content.Add(new StringContent(product), "ProductType");
        AddFile(content, workbook, "mrls.xlsx");
        return content;
    }

    private static void AddFile(MultipartFormDataContent content, byte[] workbook, string fileName)
    {
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "File", fileName);
    }

    private static byte[] CreateWorkbook(IReadOnlyList<string?[]> rows)
    {
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheetData = new XElement(spreadsheet + "sheetData");

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new XElement(spreadsheet + "row", new XAttribute("r", rowIndex + 1));
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex];
                if (value is null) continue;

                row.Add(new XElement(
                    spreadsheet + "c",
                    new XAttribute("r", $"{ColumnName(columnIndex)}{rowIndex + 1}"),
                    new XAttribute("t", "inlineStr"),
                    new XElement(spreadsheet + "is", new XElement(spreadsheet + "t", value))));
            }

            sheetData.Add(row);
        }

        var document = new XDocument(new XElement(spreadsheet + "worksheet", sheetData));
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using var entryStream = entry.Open();
            document.Save(entryStream);
        }

        return output.ToArray();
    }

    private static string ColumnName(int zeroBasedIndex)
    {
        var value = zeroBasedIndex + 1;
        var name = string.Empty;
        while (value > 0)
        {
            value--;
            name = (char)('A' + (value % 26)) + name;
            value /= 26;
        }

        return name;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Succeeded, envelope.Error?.Message);
        return envelope.Data.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
