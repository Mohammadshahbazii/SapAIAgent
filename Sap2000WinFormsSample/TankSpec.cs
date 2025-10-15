using System.Text.Json;

namespace Sap2000WinFormsSample
{
    public class TankSpec
    {
        public string type { get; set; }
        public Units units { get; set; }
        public Geometry geometry { get; set; }
        public Materials materials { get; set; }
        public Loads loads { get; set; }
        public double foundationElevation { get; set; }
        public bool fixBase { get; set; } = true;

        public static TankSpec FromJson(string json) =>
            JsonSerializer.Deserialize<TankSpec>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    public class Units
    {
        public string length { get; set; } // "m" | "mm" | "ft" | "in"
        public string force { get; set; }  // "kN" | "N" | "kip" | "lb"
    }

    public class Geometry
    {
        public double diameter { get; set; }
        public double height { get; set; }
        public double shellThickness { get; set; }
        public int numWallSegments { get; set; }
        public int numHeightSegments { get; set; }
        public double length { get; set; }
        public double radius { get; set; }
        public double roofRise { get; set; }
    }

    public class Materials
    {
        public string steelGrade { get; set; }
        public double yieldStress { get; set; } // e.g., MPa
    }

    public class Loads
    {
        public double liquidHeight { get; set; }
        public double unitWeight { get; set; } // kN/m^3 if SI
        public string unitWeightUnits { get; set; }
        public double density { get; set; }
        public string densityUnits { get; set; }
        public double internalPressureKPa { get; set; }
    }
}
