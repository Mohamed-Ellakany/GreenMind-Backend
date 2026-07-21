using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class AgentRequest
    {
        public string Mode { get; set; } = null!; 
        public ExportPlanDto ExportPlan { get; set; } = null!;
        public PlanningWindowDto PlanningWindow { get; set; } = null!;
        public TaskContextDto? TaskContext { get; set; }
        public SoilMeasurementDto? LatestSoilMeasurement { get; set; }
    }
}
