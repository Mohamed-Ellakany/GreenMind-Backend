using System.ComponentModel;
using System.Text;
using Greenmind.Application.Contracts.Persistence;
using Greenmind.Domain.Consts;
using Greenmind.Domain.Consts.Enums;
using Microsoft.SemanticKernel;

namespace Greenmind.Infrastructure.Agents.Plugins
{
    public class ComplianceRagPlugin
    {
        private readonly IComplianceDocumentRepository _repo;

        public ComplianceRagPlugin(IComplianceDocumentRepository repo) => _repo = repo;

        [KernelFunction("search_quality_grading_rules")]
        [Description("Searches UNECE quality/grading/sizing/packaging standards for the crop (e.g. minimum size, class Extra/I/II requirements, tolerances). Use before generating QualityCheck, Harvest, or ExportPrep tasks.")]
        public async Task<string> SearchQualityGradingAsync(
            [Description("What you need to know, e.g. 'minimum size for Extra class strawberries'")] string query,
            [Description("Crop name, e.g. 'Strawberry'")] string crop)
        {
            return await SearchAsync(query, ComplianceCategory.QualityGrading, crop, destination: null);
        }

        [KernelFunction("search_phytosanitary_rules")]
        [Description("Searches EU phytosanitary/plant-health import requirements (regulated pests, certificates needed to enter the EU). Use before generating ExportPrep tasks or when assessing export eligibility.")]
        public async Task<string> SearchPhytosanitaryAsync(
            [Description("What you need to know, e.g. 'phytosanitary certificate requirements for strawberries entering the EU'")] string query,
            [Description("Crop name, e.g. 'Strawberry'")] string crop)
        {
            return await SearchAsync(query, ComplianceCategory.Phytosanitary, crop, destination: null);
        }

        [KernelFunction("search_pesticide_residue_rules")]
        [Description("Searches pesticide MRL (maximum residue level) and pre-harvest interval (PHI) rules. Use before generating or validating PestControl tasks to ensure the application date and product respect EU limits.")]
        public async Task<string> SearchPesticideResidueAsync(
            [Description("What you need to know, e.g. 'pre-harvest interval for fungicide X on strawberries'")] string query,
            [Description("Crop name, e.g. 'Strawberry'")] string crop)
        {
            return await SearchAsync(query, ComplianceCategory.PesticideResidue, crop, destination: null);
        }

        [KernelFunction("search_disease_treatment")]
        [Description("Searches agronomic treatment protocols for a detected plant disease. Use after the CV model identifies a disease, to build a treatment plan.")]
        public async Task<string> SearchDiseaseTreatmentAsync(
            [Description("Disease name as detected by the CV model, e.g. 'Botrytis cinerea (gray mold)'")] string diseaseName,
            [Description("Crop name, e.g. 'Strawberry'")] string crop)
        {
            return await SearchAsync(diseaseName, ComplianceCategory.DiseaseTreatment, crop, destination: null);
        }

        private async Task<string> SearchAsync(string query, ComplianceCategory category, string? crop, string? destination)
        {
            var results = await _repo.SearchTextAsync(
                query,
                category: category,
                crop: CropCatalog.NormalizeCanonicalKey(crop),
                destination: destination,
                topK: 5);

            if (results.Count == 0)
                return "No compliance knowledge base entries found for this query yet.";

            var sb = new StringBuilder();
            foreach (var result in results)
            {
                sb.AppendLine($"[Source: {result.Document.SourceName}" +
                               (result.Document.SectionReference is not null ? $" - {result.Document.SectionReference}" : "") +
                               $" | relevance: {result.Score:F2}]");
                sb.AppendLine(result.Document.Content);
                sb.AppendLine("---");
            }

            return sb.ToString();
        }
    }
}
