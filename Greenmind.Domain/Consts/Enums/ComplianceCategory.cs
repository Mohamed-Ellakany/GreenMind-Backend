using System;

namespace Greenmind.Domain.Consts.Enums
{
    /// <summary>
    /// Distinguishes the *type* of compliance knowledge a chunk belongs to.
    /// Kept separate because they answer fundamentally different questions:
    /// - QualityGrading   -> "is the produce physically graded/sized/packaged correctly?" (UNECE FFV standards)
    /// - PesticideResidue -> "is pesticide residue within legal limits / PHI respected?" (EU Reg 396/2005)
    /// - Phytosanitary    -> "is the crop even allowed to enter the EU?" (EU Reg 2016/2031, 2019/2072)
    /// - DiseaseTreatment -> "how do we treat a disease the CV model detected?" (EPPO / IPM guides)
    /// </summary>
    public enum ComplianceCategory
    {
        QualityGrading,
        PesticideResidue,
        Phytosanitary,
        DiseaseTreatment,
        CropCalendar,
        AgronomicGuidance,
        ExportMarketRequirements
    }
}
