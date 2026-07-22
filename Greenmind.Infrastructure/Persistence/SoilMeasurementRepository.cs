using Greenmind.Application.Dtos.AI;
using Greenmind.Application.Interfaces.AI;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Infrastructure.Persistence
{
    public class SoilMeasurementRepository : ISoilMeasurementRepository
    {
        private readonly GreenmindDbContext _db;
        public SoilMeasurementRepository(GreenmindDbContext db) => _db = db;

        public async Task<SoilMeasurementDto?> GetLatestAsync(int exportPlanId)
        {
            var entity = await _db.SoilMeasurements
                .Where(s => s.ExportPlanId == exportPlanId)
                .OrderByDescending(s => s.MeasurementDate)
                .FirstOrDefaultAsync();

            return entity is null ? null : new SoilMeasurementDto
            {
                MeasurementDate = entity.MeasurementDate,
                Ph = entity.PH,
                Ec = entity.EC,
                Nitrogen = entity.Nitrogen,
                Phosphorus = entity.Phosphorus,
                Potassium = entity.Potassium,
                Moisture = entity.Moisture
            };
        }

        public async Task<List<SoilMeasurementDto>> GetHistoryAsync(int exportPlanId, DateOnly since)
        {
            var entities = await _db.SoilMeasurements
                .Where(s => s.ExportPlanId == exportPlanId && s.MeasurementDate >= since)
                .OrderBy(s => s.MeasurementDate)
                .ToListAsync();

            return entities.Select(e => new SoilMeasurementDto
            {
                MeasurementDate = e.MeasurementDate,
                Ph = e.PH,
                Ec = e.EC,
                Nitrogen = e.Nitrogen,
                Phosphorus = e.Phosphorus,
                Potassium = e.Potassium,
                Moisture = e.Moisture
            }).ToList();
        }
    }
}
