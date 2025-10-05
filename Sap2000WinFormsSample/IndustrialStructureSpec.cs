using System.Collections.Generic;
using System.Text.Json;

namespace Sap2000WinFormsSample
{
    public class IndustrialStructureSpec
    {
        public string name { get; set; }
        public Units units { get; set; }
        public double span { get; set; } = 24.0;
        public double baySpacing { get; set; } = 6.0;
        public int bayCount { get; set; } = 5;
        public double length { get; set; }
        public int aisleCount { get; set; } = 1;
        public double baseElevation { get; set; } = 0.0;
        public double eaveHeight { get; set; } = 8.0;
        public double ridgeHeight { get; set; } = 10.0;
        public bool fixBase { get; set; } = true;
        public IndustrialRoofSpec roof { get; set; }
            = new IndustrialRoofSpec();
        public IndustrialBracingSpec bracing { get; set; }
            = new IndustrialBracingSpec();
        public IndustrialCraneSpec crane { get; set; }
            = new IndustrialCraneSpec();
        public IndustrialFrameSpec frames { get; set; }
            = new IndustrialFrameSpec();
        public List<IndustrialBayOverride> bayOverrides { get; set; }

            = new List<IndustrialBayOverride>();

        public static IndustrialStructureSpec FromJson(string json) =>
            JsonSerializer.Deserialize<IndustrialStructureSpec>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    public class IndustrialRoofSpec
    {
        public string system { get; set; } = "PortalRafter"; // PortalRafter | Truss | Flat
        public double slopeRatio { get; set; } = 0.2; // rise per half span
        public bool addPurlins { get; set; } = true;
        public int trussPanels { get; set; } = 4;
        public string purlinSection { get; set; } = "RoofPurlin200";
        public double ridgeOffset { get; set; } = 0.0; // allow asymmetry
    }

    public class IndustrialBracingSpec
    {
        public bool portalBracing { get; set; } = true;
        public bool roofBracing { get; set; } = true;
        public bool longitudinalBracing { get; set; } = true;
        public string braceSection { get; set; } = "Brace200";
    }

    public class IndustrialCraneSpec
    {
        public bool enabled { get; set; }
        public double runwayElevation { get; set; } = 6.5;
        public string runwaySection { get; set; } = "CraneGirder400";
        public double inset { get; set; } = 0.6; // distance from column face
    }

    public class IndustrialFrameSpec
    {
        public IndustrialFrameSection columns { get; set; } = new IndustrialFrameSection
        {
            name = "Column450x350",
            depth = 0.45,
            width = 0.35,
            material = "A992Fy50"
        };

        public IndustrialFrameSection rafters { get; set; } = new IndustrialFrameSection
        {
            name = "Rafter400x300",
            depth = 0.40,
            width = 0.30,
            material = "A992Fy50"
        };

        public IndustrialFrameSection craneGirders { get; set; } = new IndustrialFrameSection
        {
            name = "CraneGirder500x300",
            depth = 0.50,
            width = 0.30,
            material = "A572Gr50"
        };

        public IndustrialFrameSection purlins { get; set; } = new IndustrialFrameSection
        {
            name = "Purlin200x100",
            depth = 0.20,
            width = 0.10,
            material = "A36"
        };

        public IndustrialFrameSection braces { get; set; } = new IndustrialFrameSection
        {
            name = "Brace200x200",
            depth = 0.20,
            width = 0.20,
            material = "A572Gr50"
        };
    }

    public class IndustrialFrameSection
    {
        public string name { get; set; }
        public string material { get; set; }
        public double depth { get; set; } = 0.3;
        public double width { get; set; } = 0.3;
    }

    public class IndustrialBayOverride
    {
        public int bayIndex { get; set; }
        public double? baySpacing { get; set; }
        public bool? addPortalBracing { get; set; }
        public bool? addCrane { get; set; }
    }
}
