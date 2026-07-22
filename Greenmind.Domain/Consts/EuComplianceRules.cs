using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Domain.Consts
{
    public static class EuComplianceRules
    {
        // Pre-harvest interval بالأيام لكل محصول (المدة اللي لازم تعدي بين آخر رش وبين الحصاد)
        public static readonly Dictionary<string, int> PreHarvestIntervalDays = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Strawberry", 15 }
           
        };

        public const string GeneralGuidelines = """
            EU export compliance rules (Regulation EC No 396/2005 and related):
            - Pesticide residues must not exceed the EU Maximum Residue Level (MRL); if no specific MRL
              exists for a pesticide/crop combination, the default limit is 0.01 mg/kg.
            - Any PestControl task must respect the crop's pre-harvest interval (PHI) — no pesticide
              application is allowed within the PHI window before the expected harvest date.
            - ExportPrep and QualityCheck tasks should verify documentation of pesticide application
              dates and dosages to demonstrate MRL compliance for customs/phytosanitary certification.
            - Prefer integrated pest management (IPM) practices over blanket pesticide use where possible,
              since some EU member states apply stricter national limits than the harmonized EU MRL.
            """;
    }
}