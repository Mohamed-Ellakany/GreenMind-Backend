using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class PlanningWindowDto
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int DaysToGenerate { get; set; }
    }
}
