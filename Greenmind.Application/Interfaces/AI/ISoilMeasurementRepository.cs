using Greenmind.Application.Dtos.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Interfaces.AI
{
    public interface ISoilMeasurementRepository
    {
        Task<SoilMeasurementDto?> GetLatestAsync(int exportPlanId);
        Task<List<SoilMeasurementDto>> GetHistoryAsync(int exportPlanId, DateOnly since);

    }
}
