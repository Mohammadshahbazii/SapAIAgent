using System.Collections.Generic;
using System.Text.Json;

namespace Sap2000WinFormsSample
{
    public class BridgeDesignSpec
    {
        public string name { get; set; }
        public Units units { get; set; }
        public string bridgeType { get; set; } = "Girder"; // Girder | CableStayed | Arch | Truss
        public List<BridgeSpanSpec> spans { get; set; } = new List<BridgeSpanSpec>();
        public double deckWidth { get; set; } = 12.0;
        public int girders { get; set; } = 4;
        public int segmentsPerSpan { get; set; } = 8;
        public double deckElevation { get; set; } = 12.0;
        public BridgeSupportSpec supports { get; set; } = new BridgeSupportSpec();
        public BridgeSuperstructureSpec superstructure { get; set; } = new BridgeSuperstructureSpec();
        public BridgeAnalysisSpec analyses { get; set; } = new BridgeAnalysisSpec();

        public static BridgeDesignSpec FromJson(string json) =>
            JsonSerializer.Deserialize<BridgeDesignSpec>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    public class BridgeSpanSpec
    {
        public double length { get; set; } = 30.0;
        public bool hasExpansionJoint { get; set; }
    }

    public class BridgeSupportSpec
    {
        public double pierHeight { get; set; } = 10.0;
        public bool fixBase { get; set; } = true;
        public double foundationElevation { get; set; } = 0.0;
        public int columnsPerPier { get; set; } = 2;
    }

    public class BridgeSuperstructureSpec
    {
        public string girderSection { get; set; } = "Girder900x300";
        public string crossBeamSection { get; set; } = "CrossBeam600x250";
        public string cableSection { get; set; } = "CableRod120";
        public string archSection { get; set; } = "ArchBox800x400";
        public string trussSection { get; set; } = "TrussMember400x250";
        public double towerHeight { get; set; } = 35.0;
        public double trussHeight { get; set; } = 6.0;
        public double archRiseRatio { get; set; } = 0.2; // rise relative to span
        public bool addDiaphragms { get; set; } = true;
    }

    public class BridgeAnalysisSpec
    {
        public bool enableStageConstruction { get; set; } = true;
        public bool enableMovingLoad { get; set; } = true;
        public bool enableSeismic { get; set; } = true;
    }
}
