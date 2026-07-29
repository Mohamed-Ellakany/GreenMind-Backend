using System.ComponentModel;
using System.Text.Json;
using Greenmind.Application.Contracts.Regulatory;
using Microsoft.SemanticKernel;

namespace Greenmind.Infrastructure.Agents.Plugins;

public sealed class RegulatoryDataPlugin(IRegulatoryKnowledgeQueryService queryService)
{
    [KernelFunction("find_active_substance")]
    [Description("Searches the administrator-approved active-substance dataset for one region. Returns approval status, approval expiry, legislation, remarks and functions. The data is read-only.")]
    public async Task<string> FindActiveSubstanceAsync(
        [Description("Region name exactly as used by GreenMind, for example European Union")] string regionName,
        [Description("Active substance name, CAS number, or external active-substance id")] string search,
        CancellationToken cancellationToken = default)
    {
        var results = await queryService.FindActiveSubstancesAsync(regionName, search, cancellationToken);
        return JsonSerializer.Serialize(results);
    }

    [KernelFunction("find_product_mrl")]
    [Description("Searches the administrator-approved maximum residue limits for one region and product. Values are in mg/kg and the result states whether a value is an analytical detection limit. The data is read-only.")]
    public async Task<string> FindProductMrlAsync(
        [Description("Region name exactly as used by GreenMind, for example European Union")] string regionName,
        [Description("Product or crop name exactly as used by GreenMind, for example Strawberry")] string productType,
        [Description("Residue name or external pesticide id")] string search,
        CancellationToken cancellationToken = default)
    {
        var results = await queryService.FindProductMrlsAsync(
            regionName,
            productType,
            search,
            cancellationToken);
        return JsonSerializer.Serialize(results);
    }
}
