using System.Collections.Generic;
using System.Text.Json;

namespace Sap2000WinFormsSample
{
    public class BuildingDesignSpec
    {
        public string name { get; set; }
        public Units units { get; set; }
        public BuildingLayout layout { get; set; }
        public List<StorySpec> stories { get; set; }
        public BuildingMaterials materials { get; set; }
        public LateralSystemSpec lateralSystem { get; set; }
        public DeckSpec deck { get; set; }
        public DesignCodeSpec designCodes { get; set; }
        public AnalysisSpec analyses { get; set; }

        public static BuildingDesignSpec FromJson(string json) =>
            JsonSerializer.Deserialize<BuildingDesignSpec>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    public class BuildingLayout
    {
        public int baysX { get; set; } = 3;
        public int baysY { get; set; } = 2;
        public double baySpacingX { get; set; } = 6.0;
        public double baySpacingY { get; set; } = 6.0;
        public double baseElevation { get; set; } = 0.0;
    }

    public class StorySpec
    {
        public string name { get; set; }
        public double height { get; set; } = 3.2;
        public string beamSection { get; set; }
        public string columnSection { get; set; }
        public string braceSection { get; set; }
        public string shearWallPanel { get; set; }
    }

    public class BuildingMaterials
    {
        public string steelMaterial { get; set; } = "A992Fy50";
        public string concreteMaterial { get; set; } = "Concrete4000";
        public FrameSectionDefinition steelColumn { get; set; }
        public FrameSectionDefinition steelBeam { get; set; }
        public FrameSectionDefinition brace { get; set; }
        public FrameSectionDefinition concreteColumn { get; set; }
        public FrameSectionDefinition concreteBeam { get; set; }
        public ShellSectionDefinition shearWall { get; set; }
        public ShellSectionDefinition slab { get; set; }
    }

    public class FrameSectionDefinition
    {
        public string name { get; set; }
        public string material { get; set; }
        public double depth { get; set; }
        public double width { get; set; }
        public bool hollow { get; set; }
    }

    public class ShellSectionDefinition
    {
        public string name { get; set; }
        public string material { get; set; }
        public double thickness { get; set; }
    }

    public class LateralSystemSpec
    {
        public string systemType { get; set; } = "MomentFrame"; // MomentFrame | BracedFrame | ShearWall | Dual
        public bool addBracesInBothDirections { get; set; } = false;
        public bool addShearWallCore { get; set; } = false;
        public double shearWallCoreSize { get; set; } = 6.0;
    }

    public class DeckSpec
    {
        public string type { get; set; } = "Composite"; // Composite | Concrete
        public string propertyName { get; set; }
        public double thickness { get; set; } = 0.15;
        public string material { get; set; }
    }

    public class DesignCodeSpec
    {
        public string steel { get; set; } = "AISC360-16";
        public string concrete { get; set; } = "ACI318-19";
        public string compositeBeam { get; set; } = "AISC360-16";
    }

    public class AnalysisSpec
    {
        public bool requestModal { get; set; } = true;
        public int modalModes { get; set; } = 12;
        public bool requestResponseSpectrum { get; set; } = true;
        public bool requestTimeHistory { get; set; } = false;
        public bool requestPushover { get; set; } = false;
        public bool requestPlasticHinges { get; set; } = false;
    }
}
