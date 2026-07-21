using System;
using System.Collections.Generic;
using System.Text;

namespace Greenmind.Application.Dtos.AI
{
    public class SoilMeasurementDto
    {
        public DateOnly MeasurementDate { get; set; }
        public double Ph { get; set; }
        public double Ec { get; set; }
        public double Nitrogen { get; set; }
        public double Phosphorus { get; set; }
        public double Potassium { get; set; }
        public double Moisture { get; set; }
    }
}
