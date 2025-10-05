using System.Text.Json;

namespace Sap2000WinFormsSample
{
    public class SpecialStructureSpec
    {
        public string name { get; set; }
        public Units units { get; set; }
        public string structureType { get; set; } = "SpaceFrame"; // SpaceFrame | Dome | Membrane | Tower | CoolingTower | TelecomTower
        public double radius { get; set; } = 25.0;
        public double height { get; set; } = 18.0;
        public int rings { get; set; } = 5;
        public int segments { get; set; } = 24;
        public double baseElevation { get; set; } = 0.0;
        public bool fixBase { get; set; } = true;
        public SpecialTowerSpec tower { get; set; } = new SpecialTowerSpec();
        public SpecialMembraneSpec membrane { get; set; } = new SpecialMembraneSpec();
        public SpecialSeismicSpec seismic { get; set; } = new SpecialSeismicSpec();

        public static SpecialStructureSpec FromJson(string json) =>
            JsonSerializer.Deserialize<SpecialStructureSpec>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    public class SpecialTowerSpec
    {
        public int sides { get; set; } = 4;
        public int segments { get; set; } = 8;
        public double taperRatio { get; set; } = 0.15;
        public string legSection { get; set; } = "TowerLeg350x250";
        public string braceSection { get; set; } = "TowerBrace200x150";
    }

    public class SpecialMembraneSpec
    {
        public string membraneSection { get; set; } = "MembranePTFE";
        public string edgeCableSection { get; set; } = "EdgeCable90";
        public double prestress { get; set; } = 2.0; // kN/m
    }

    public class SpecialSeismicSpec
    {
        public bool addBaseIsolators { get; set; }
        public bool addDampers { get; set; }
        public double targetPeriod { get; set; } = 2.0;
    }
}
