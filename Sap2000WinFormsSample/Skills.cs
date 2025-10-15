
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using SAP2000v1;
using Sap2000WinFormsSample;

namespace Sap2000WinFormsSample
{
    public interface ISkill
    {
        string Name { get; } // action name as used in plan
        string Description { get; } // brief description for the LLM
        string ParamsSchema { get; } // JSON-like example for guidance
        IEnumerable<string> DocumentationReferences { get; }
        string Execute(cSapModel model, Dictionary<string, object> args);
    }

    public class SkillRegistry
    {
        private readonly Dictionary<string, ISkill> _skills = new Dictionary<string, ISkill>(StringComparer.OrdinalIgnoreCase);

        public void Register(ISkill skill) => _skills[skill.Name] = skill;
        public bool TryGet(string name, out ISkill skill) => _skills.TryGetValue(name, out skill);
        public IEnumerable<ISkill> All() => _skills.Values;
    }

    // --------- Some concrete skills ---------

    public class InitializeBlankModelSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.InitializeNewModel",
            "cSapModel.File.NewBlank",
            "cSapModel.SetPresentUnits"
        };

        public string Name => "InitializeBlankModel";
        public string Description => "Create a new blank model in a specified unit system.";
        public string ParamsSchema => @"{ ""units"": ""kN_m_C|N_mm_C|kip_ft_F|kip_in_F"" }";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var unitsStr = args.TryGetValue("units", out var u) ? Convert.ToString(u) : "kN_m_C";
            eUnits units;
            if (unitsStr == "N_mm_C")
                units = eUnits.N_mm_C;
            else if (unitsStr == "kip_ft_F")
                units = eUnits.kip_ft_F;
            else if (unitsStr == "kip_in_F")
                units = eUnits.kip_in_F;
            else
                units = eUnits.kN_m_C;
            int ret = model.InitializeNewModel(units);
            if (ret != 0) throw new ApplicationException("InitializeNewModel failed.");

            ret = model.File.NewBlank();
            if (ret != 0) throw new ApplicationException("File.NewBlank failed.");

            ret = model.SetPresentUnits(units);
            if (ret != 0) throw new ApplicationException("SetPresentUnits failed.");

            return $"Initialized new blank model with units set to {units}.";
        }
    }

    public class SetUnitsSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.SetPresentUnits"
        };

        public string Name => "SetUnits";
        public string Description => "Set present units for subsequent API calls.";
        public string ParamsSchema => @"{ ""units"": ""kN_m_C|N_mm_C|kip_ft_F|kip_in_F"" }";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var unitsStr = args.TryGetValue("units", out var u) ? Convert.ToString(u) : "kN_m_C";
            eUnits units;
            if (unitsStr == "N_mm_C")
                units = eUnits.N_mm_C;
            else if (unitsStr == "kip_ft_F")
                units = eUnits.kip_ft_F;
            else if (unitsStr == "kip_in_F")
                units = eUnits.kip_in_F;
            else
                units = eUnits.kN_m_C;

            int ret = model.SetPresentUnits(units);
            if (ret != 0) throw new ApplicationException("SetPresentUnits failed.");
            return $"Units set to {units}.";
        }
    }

    public class BuildCylindricalReservoirSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.SetPresentUnits",
            "cSapModel.PointObj.AddCartesian",
            "cSapModel.PointObj.SetRestraint",
            "cSapModel.FrameObj.AddByPoint",
            "cSapModel.LoadPatterns.Add",
            "cSapModel.PointObj.SetLoadForce",
            "cSapModel.Analyze.SetRunCaseFlag"
        };

        public string Name => "BuildCylindricalReservoir";
        public string Description => "Create cylindrical reservoir frames (rings + verticals).";
        public string ParamsSchema => @"{
  ""type"": ""VerticalCylindrical|Horizontal|Spherical"",
  ""units"": ""kN_m_C"",
  ""geometry"": { ""diameter"": 10.0, ""height"": 8.0, ""length"": 14.0 },
  ""shellThickness"": 0.2,
  ""numWallSegments"": 24, ""numHeightSegments"": 8,
  ""foundationElevation"": 0.0,
  ""loads"": { ""liquidHeight"": 6.0, ""unitWeight"": 9.81, ""unitWeightUnits"": ""kN/m3|kg/m3"", ""density"": 1000 },
  ""fixBase"": true
}";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            double D = GetD(args, "diameter", 10);
            double H = GetD(args, "height", 8);
            int nCirc = (int)GetD(args, "numWallSegments", 24);
            int nZ = (int)GetD(args, "numHeightSegments", 8);
            double z0 = GetD(args, "foundationElevation", 0);
            double shellThickness = GetD(args, "shellThickness", 0);
            double liquidHeight = GetD(args, "liquidHeight", 0);
            double unitWeight = GetD(args, "unitWeight", 0);
            string unitWeightUnits = GetString(args, "unitWeightUnits", null);
            double density = GetD(args, "density", 0);
            string densityUnits = GetString(args, "densityUnits", null);
            double internalPressure = GetD(args, "internalPressureKPa", 0);
            bool fixBase = GetBool(args, "fixBase", true);
            double vesselLength = GetD(args, "length", 0);
            double roofRise = GetD(args, "roofRise", 0);

            if (args.TryGetValue("geometry", out var geometryObj) && geometryObj != null)
            {
                if (geometryObj is Dictionary<string, object> geometryDict)
                {
                    D = GetD(geometryDict, "diameter", D);
                    H = GetD(geometryDict, "height", H);
                    shellThickness = GetD(geometryDict, "shellThickness", shellThickness);
                    nCirc = (int)GetD(geometryDict, "numWallSegments", nCirc);
                    nZ = (int)GetD(geometryDict, "numHeightSegments", nZ);
                    vesselLength = GetD(geometryDict, "length", vesselLength);
                    roofRise = GetD(geometryDict, "roofRise", roofRise);
                }
                else if (geometryObj is JsonElement geometryElement && geometryElement.ValueKind == JsonValueKind.Object)
                {
                    if (geometryElement.TryGetProperty("diameter", out var v)) D = GetD(v, D);
                    if (geometryElement.TryGetProperty("height", out var h)) H = GetD(h, H);
                    if (geometryElement.TryGetProperty("shellThickness", out var s)) shellThickness = GetD(s, shellThickness);
                    if (geometryElement.TryGetProperty("numWallSegments", out var n)) nCirc = (int)GetD(n, nCirc);
                    if (geometryElement.TryGetProperty("numHeightSegments", out var hs)) nZ = (int)GetD(hs, nZ);
                    if (geometryElement.TryGetProperty("length", out var lenProp)) vesselLength = GetD(lenProp, vesselLength);
                    if (geometryElement.TryGetProperty("roofRise", out var roofProp)) roofRise = GetD(roofProp, roofRise);
                }
            }

            if (args.TryGetValue("loads", out var loadsObj) && loadsObj != null)
            {
                if (loadsObj is Dictionary<string, object> loadDict)
                {
                    liquidHeight = GetD(loadDict, "liquidHeight", liquidHeight);
                    unitWeight = GetD(loadDict, "unitWeight", unitWeight);
                    unitWeightUnits = GetString(loadDict, "unitWeightUnits", unitWeightUnits);
                    density = GetD(loadDict, "density", density);
                    densityUnits = GetString(loadDict, "densityUnits", densityUnits);
                    internalPressure = GetD(loadDict, "internalPressureKPa", internalPressure);
                }
                else if (loadsObj is JsonElement loadElement && loadElement.ValueKind == JsonValueKind.Object)
                {
                    if (loadElement.TryGetProperty("liquidHeight", out var v)) liquidHeight = GetD(v, liquidHeight);
                    if (loadElement.TryGetProperty("unitWeight", out var u)) unitWeight = GetD(u, unitWeight);
                    if (loadElement.TryGetProperty("unitWeightUnits", out var uu)) unitWeightUnits = GetString(uu, unitWeightUnits);
                    if (loadElement.TryGetProperty("density", out var d)) density = GetD(d, density);
                    if (loadElement.TryGetProperty("densityUnits", out var du)) densityUnits = GetString(du, densityUnits);
                    if (loadElement.TryGetProperty("internalPressureKPa", out var ip)) internalPressure = GetD(ip, internalPressure);
                }
            }

            var specUnits = new Units { length = "m", force = "kN" };

            if (args.TryGetValue("units", out var unitsObj) && unitsObj != null)
            {
                var resolvedUnits = TryResolveUnits(unitsObj, ref specUnits);
                if (resolvedUnits.HasValue)
                {
                    int retU = model.SetPresentUnits(resolvedUnits.Value);
                    if (retU != 0) throw new ApplicationException("SetPresentUnits in builder failed.");
                }
            }

            // Build via the helper you already have
            Loads loads = null;
            if (liquidHeight > 0 || unitWeight > 0 || density > 0 || internalPressure > 0)
            {
                loads = new Loads
                {
                    liquidHeight = liquidHeight,
                    unitWeight = unitWeight,
                    unitWeightUnits = unitWeightUnits,
                    density = density,
                    densityUnits = densityUnits,
                    internalPressureKPa = internalPressure
                };
            }

            var spec = new TankSpec
            {
                units = specUnits,
                geometry = new Geometry
                {
                    diameter = D,
                    height = H,
                    length = vesselLength,
                    numWallSegments = nCirc,
                    numHeightSegments = nZ,
                    shellThickness = shellThickness,
                    roofRise = roofRise
                },
                loads = loads,
                foundationElevation = z0,
                fixBase = fixBase,
                type = args.TryGetValue("type", out var typeObj) ? Convert.ToString(typeObj) : null
            };

            var result = SapBuilder.BuildPressureVessel(model, spec);
            string resolvedType = string.IsNullOrWhiteSpace(spec.type) ? "VerticalCylindrical" : spec.type;
            return $"Pressure vessel ({resolvedType}) generated. joints={result.jointCount}, frameMembers={result.frameMembers}.";
        }



        static double GetD(Dictionary<string, object> dict, string key, double defVal = 0)
        {
            if (dict == null || !dict.TryGetValue(key, out var v) || v == null) return defVal;
            if (v is double dd) return dd;
            if (v is float ff) return ff;
            if (v is int ii) return ii;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture),
                                NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return defVal;
        }

        static double GetD(JsonElement el, double defVal = 0)
        {
            try
            {
                if (el.ValueKind == JsonValueKind.Number)
                {
                    return el.GetDouble();
                }
                else if (el.ValueKind == JsonValueKind.String)
                {
                    double parsed;
                    if (double.TryParse(el.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out parsed))
                        return parsed;
                }
            }
            catch { }
            return defVal;
        }

        static string GetString(Dictionary<string, object> dict, string key, string defVal = null)
        {
            if (dict == null || !dict.TryGetValue(key, out var v) || v == null) return defVal;
            var s = Convert.ToString(v, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(s) ? defVal : s;
        }

        static string GetString(JsonElement el, string defVal = null)
        {
            try
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    return string.IsNullOrWhiteSpace(s) ? defVal : s;
                }
                return defVal;
            }
            catch { return defVal; }
        }

        static bool GetBool(Dictionary<string, object> dict, string key, bool defVal = false)
        {
            if (dict == null || !dict.TryGetValue(key, out var v) || v == null) return defVal;
            if (v is bool b) return b;
            var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes" || s == "on") return true;
            if (s == "false" || s == "0" || s == "no" || s == "off") return false;
            return defVal;
        }

        // Optional: resolve "units" arg and also populate your specUnits object
        static eUnits? TryResolveUnits(object unitsObj, ref Units specUnits)
        {
            if (unitsObj == null) return null;
            var s = Convert.ToString(unitsObj)?.Trim();
            if (string.IsNullOrEmpty(s)) return null;

            // normalize tokens
            var t = s.Replace("-", "_").Replace(" ", "_").ToLowerInvariant();

            // map a few common forms
            if (t.Contains("n_mm")) { specUnits.length = "mm"; specUnits.force = "N"; return eUnits.N_mm_C; }
            if (t.Contains("kip_in")) { specUnits.length = "in"; specUnits.force = "kip"; return eUnits.kip_in_F; }
            if (t.Contains("kip_ft")) { specUnits.length = "ft"; specUnits.force = "kip"; return eUnits.kip_ft_F; }

            // default SI
            specUnits.length = "m"; specUnits.force = "kN";
            return eUnits.kN_m_C;
        }


        private static bool GetBool(JsonElement element, bool defaultValue)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (element.TryGetDouble(out var val)) return Math.Abs(val) > double.Epsilon;
                    break;
                case JsonValueKind.String:
                    var str = element.GetString();
                    if (bool.TryParse(str, out var parsedBool)) return parsedBool;
                    if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble)) return Math.Abs(parsedDouble) > double.Epsilon;
                    break;
            }
            return defaultValue;
        }


        private static eUnits? ResolveUnitsFromCode(string code, ref Units specUnits)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code.Trim();

            switch (code)
            {
                case "N_mm_C":
                    specUnits = new Units { length = "mm", force = "N" };
                    return eUnits.N_mm_C;
                case "kip_ft_F":
                    specUnits = new Units { length = "ft", force = "kip" };
                    return eUnits.kip_ft_F;
                case "kip_in_F":
                    specUnits = new Units { length = "in", force = "kip" };
                    return eUnits.kip_in_F;
                case "kN_m_C":
                default:
                    specUnits = new Units { length = "m", force = "kN" };
                    return eUnits.kN_m_C;
            }
        }

        private static eUnits? ResolveUnitsFromComponents(string length, string force, ref Units specUnits)
        {
            if (string.IsNullOrWhiteSpace(length) || string.IsNullOrWhiteSpace(force)) return null;

            length = length.Trim();
            force = force.Trim();

            if (string.Equals(length, "mm", StringComparison.OrdinalIgnoreCase) && string.Equals(force, "N", StringComparison.OrdinalIgnoreCase))
            {
                specUnits = new Units { length = "mm", force = "N" };
                return eUnits.N_mm_C;
            }
            if (string.Equals(length, "ft", StringComparison.OrdinalIgnoreCase) && string.Equals(force, "kip", StringComparison.OrdinalIgnoreCase))
            {
                specUnits = new Units { length = "ft", force = "kip" };
                return eUnits.kip_ft_F;
            }
            if (string.Equals(length, "in", StringComparison.OrdinalIgnoreCase) && string.Equals(force, "kip", StringComparison.OrdinalIgnoreCase))
            {
                specUnits = new Units { length = "in", force = "kip" };
                return eUnits.kip_in_F;
            }
            if (string.Equals(length, "m", StringComparison.OrdinalIgnoreCase) && string.Equals(force, "kN", StringComparison.OrdinalIgnoreCase))
            {
                specUnits = new Units { length = "m", force = "kN" };
                return eUnits.kN_m_C;
            }

            return null;
        }

        private static void UpdateUnitsForSpec(eUnits units, ref Units specUnits)
        {
            switch (units)
            {
                case eUnits.N_mm_C:
                    specUnits = new Units { length = "mm", force = "N" };
                    break;
                case eUnits.kip_ft_F:
                    specUnits = new Units { length = "ft", force = "kip" };
                    break;
                case eUnits.kip_in_F:
                    specUnits = new Units { length = "in", force = "kip" };
                    break;
                default:
                    specUnits = new Units { length = "m", force = "kN" };
                    break;
            }
        }

        private static string AsString(object value)
        {
            if (value == null) return null;
            if (value is string s) return s;
            if (value is JsonElement element && element.ValueKind == JsonValueKind.String) return element.GetString();
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    // The issue arises because the JSON-like schema provided in the ParamsSchema property is being treated as C# code.  
    // To fix this, the JSON-like schema should be enclosed in a verbatim string literal (@"") to ensure it is treated as a string.  

    public class BuildMultiStoryBuildingSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.SetPresentUnits",
            "cSapModel.PointObj.AddCartesian",
            "cSapModel.PropFrame.SetRectangle",
            "cSapModel.FrameObj.AddByPoint"
        };

        public string Name => "BuildMultiStoryBuilding";
        public string Description => "Generate multi-story steel/concrete building frames, shear walls, braces, and composite slabs based on a parametric layout.";
        public string ParamsSchema => @"
       {
           ""name"": ""OfficeTower"",
           ""units"": { ""length"": ""m"", ""force"": ""kN"" },
           ""layout"": { ""baysX"": 4, ""baysY"": 3, ""baySpacingX"": 6.0, ""baySpacingY"": 7.5 },
           ""stories"": [
               { ""name"": ""L1"", ""height"": 4.2, ""beamSection"": ""B350x450"", ""columnSection"": ""C400x400"" },
               { ""name"": ""L2"", ""height"": 3.8 }
           ],
           ""lateralSystem"": { ""systemType"": ""Dual"", ""addBracesInBothDirections"": true, ""addShearWallCore"": true },
           ""deck"": { ""type"": ""Composite"", ""thickness"": 0.13 }
       }";

        public IEnumerable<string> DocumentationReferences => _docRefs;

        // The rest of the class remains unchanged.  



        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var spec = ExtractSpec(args) ?? new BuildingDesignSpec();
            var result = SapBuilder.BuildMultiStoryBuilding(model, spec);
            return $"Building generated. joints={result.jointCount}, columns={result.frameCount}, beams={result.beamCount}, braces={result.braceCount}, shearWalls={result.shearWallPanels}, deckPanels={result.deckPanels}.";
        }

        private static BuildingDesignSpec ExtractSpec(Dictionary<string, object> args)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (args == null)
                return null;

            if (args.TryGetValue("design", out var designObj))
            {
                var fromDesign = DeserializeSpec(designObj, options);
                if (fromDesign != null)
                    return fromDesign;
            }

            if (args.TryGetValue("building", out var buildingObj))
            {
                var fromBuilding = DeserializeSpec(buildingObj, options);
                if (fromBuilding != null)
                    return fromBuilding;
            }

            if (args.TryGetValue("spec", out var specObj))
            {
                var fromSpec = DeserializeSpec(specObj, options);
                if (fromSpec != null)
                    return fromSpec;
            }

            var json = JsonSerializer.Serialize(args, options);
            return DeserializeSpec(json, options);
        }

        private static BuildingDesignSpec DeserializeSpec(object input, JsonSerializerOptions options)
        {
            if (input == null)
                return null;

            if (input is BuildingDesignSpec ready)
                return ready;

            if (input is string s)
            {
                try { return BuildingDesignSpec.FromJson(s); } catch { return null; }
            }

            if (input is JsonElement element)
            {
                try { return element.Deserialize<BuildingDesignSpec>(options); } catch { return null; }
            }

            if (input is Dictionary<string, object> dict)
            {
                try
                {
                    var json = JsonSerializer.Serialize(dict, options);
                    return JsonSerializer.Deserialize<BuildingDesignSpec>(json, options);
                }
                catch
                {
                    return null;
                }
            }

            try
            {
                var json = JsonSerializer.Serialize(input, options);
                return JsonSerializer.Deserialize<BuildingDesignSpec>(json, options);
            }
            catch
            {
                return null;
            }
        }
    }

    public class BuildIndustrialStructureSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.PointObj.AddCartesian",
            "cSapModel.FrameObj.AddByPoint",
            "cSapModel.PropFrame.SetRectangle"
        };

        public string Name => "BuildIndustrialStructure";
        public string Description => "Model steel industrial sheds, heavy truss halls, and crane-ready bays with portal frames, roof purlins, and longitudinal bracing.";
        public string ParamsSchema => @"{
  ""structure"": {
     ""name"": ""HeavyWorkshop"",
     ""span"": 30.0,
     ""baySpacing"": 7.5,
     ""bayCount"": 6,
     ""aisleCount"": 1,
     ""eaveHeight"": 10.0,
     ""ridgeHeight"": 13.0,
     ""roof"": { ""system"": ""PortalRafter"", ""addPurlins"": true },
     ""crane"": { ""enabled"": true, ""runwayElevation"": 7.2 }
  }
}";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var spec = ResolveSpec(args) ?? new IndustrialStructureSpec();
            var result = SapBuilder.BuildIndustrialStructure(model, spec);
            string label = string.IsNullOrWhiteSpace(spec.name) ? "IndustrialStructure" : spec.name;
            return $"Industrial structure '{label}' generated. joints={result.jointCount}, frames={result.frameMembers}, roofMembers={result.roofMembers}, braces={result.braceMembers}, craneGirders={result.craneGirders}.";
        }

        private static IndustrialStructureSpec ResolveSpec(Dictionary<string, object> args)
        {
            if (args == null)
                return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (args.TryGetValue("structure", out var obj))
            {
                var parsed = Deserialize(obj, options);
                if (parsed != null) return parsed;
            }

            if (args.TryGetValue("spec", out var specObj))
            {
                var parsed = Deserialize(specObj, options);
                if (parsed != null) return parsed;
            }

            var json = JsonSerializer.Serialize(args, options);
            return Deserialize(json, options);
        }

        private static IndustrialStructureSpec Deserialize(object input, JsonSerializerOptions options)
        {
            if (input == null) return null;
            if (input is IndustrialStructureSpec ready) return ready;
            if (input is string s)
            {
                try { return IndustrialStructureSpec.FromJson(s); } catch { return null; }
            }
            if (input is JsonElement element)
            {
                try { return element.Deserialize<IndustrialStructureSpec>(options); } catch { return null; }
            }
            if (input is Dictionary<string, object> dict)
            {
                try
                {
                    var json = JsonSerializer.Serialize(dict, options);
                    return JsonSerializer.Deserialize<IndustrialStructureSpec>(json, options);
                }
                catch { return null; }
            }
            try
            {
                var json = JsonSerializer.Serialize(input, options);
                return JsonSerializer.Deserialize<IndustrialStructureSpec>(json, options);
            }
            catch
            {
                return null;
            }
        }
    }

    public class BuildBridgeStructureSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.PointObj.AddCartesian",
            "cSapModel.FrameObj.AddByPoint",
            "cSapModel.PropFrame.SetRectangle",
            "cSapModel.AreaObj.AddByPoint"
        };

        public string Name => "BuildBridgeStructure";
        public string Description => "Lay out girder, cable-stayed, arch, or truss bridges with configurable spans, towers, and deck width.";
        public string ParamsSchema => @"{
  ""bridge"": {
    ""bridgeType"": ""CableStayed"",
    ""deckWidth"": 18.0,
    ""segmentsPerSpan"": 12,
    ""spans"": [ { ""length"": 60.0 }, { ""length"": 110.0 }, { ""length"": 60.0 } ],
    ""supports"": { ""pierHeight"": 25.0, ""columnsPerPier"": 2 }
  }
}";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var spec = ResolveSpec(args) ?? new BridgeDesignSpec();
            var result = SapBuilder.BuildBridgeStructure(model, spec);
            string label = string.IsNullOrWhiteSpace(spec.name) ? spec.bridgeType ?? "Bridge" : spec.name;
            return $"Bridge '{label}' created. joints={result.jointCount}, deckMembers={result.deckMembers}, supports={result.supportMembers}, cables={result.cableMembers}.";
        }

        private static BridgeDesignSpec ResolveSpec(Dictionary<string, object> args)
        {
            if (args == null)
                return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (args.TryGetValue("bridge", out var bridgeObj))
            {
                var parsed = Deserialize(bridgeObj, options);
                if (parsed != null) return parsed;
            }

            if (args.TryGetValue("spec", out var specObj))
            {
                var parsed = Deserialize(specObj, options);
                if (parsed != null) return parsed;
            }

            var json = JsonSerializer.Serialize(args, options);
            return Deserialize(json, options);
        }

        private static BridgeDesignSpec Deserialize(object input, JsonSerializerOptions options)
        {
            if (input == null) return null;
            if (input is BridgeDesignSpec ready) return ready;
            if (input is string s)
            {
                try { return BridgeDesignSpec.FromJson(s); } catch { return null; }
            }
            if (input is JsonElement element)
            {
                try { return element.Deserialize<BridgeDesignSpec>(options); } catch { return null; }
            }
            if (input is Dictionary<string, object> dict)
            {
                try
                {
                    var json = JsonSerializer.Serialize(dict, options);
                    return JsonSerializer.Deserialize<BridgeDesignSpec>(json, options);
                }
                catch { return null; }
            }
            try
            {
                var json = JsonSerializer.Serialize(input, options);
                return JsonSerializer.Deserialize<BridgeDesignSpec>(json, options);
            }
            catch
            {
                return null;
            }
        }
    }

    public class BuildSpecialStructureSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.PointObj.AddCartesian",
            "cSapModel.FrameObj.AddByPoint",
            "cSapModel.AreaObj.AddByPoint"
        };

        public string Name => "BuildSpecialStructure";
        public string Description => "Generate space frames, domes, membranes, towers, or cooling towers with advanced seismic accessories.";
        public string ParamsSchema => @"{
  ""structure"": {
     ""structureType"": ""SpaceFrame"",
     ""radius"": 28.0,
     ""height"": 15.0,
     ""segments"": 32,
     ""tower"": { ""sides"": 4, ""segments"": 10 },
     ""membrane"": { ""membraneSection"": ""MembranePTFE"" }
  }
}";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var spec = ResolveSpec(args) ?? new SpecialStructureSpec();
            var result = SapBuilder.BuildSpecialStructure(model, spec);
            string label = string.IsNullOrWhiteSpace(spec.name) ? spec.structureType ?? "SpecialStructure" : spec.name;
            return $"Special structure '{label}' built. joints={result.jointCount}, frameMembers={result.frameMembers}, braces={result.braceMembers}, cables={result.cableMembers}, shells={result.shellElements}.";
        }

        private static SpecialStructureSpec ResolveSpec(Dictionary<string, object> args)
        {
            if (args == null)
                return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (args.TryGetValue("structure", out var structureObj))
            {
                var parsed = Deserialize(structureObj, options);
                if (parsed != null) return parsed;
            }

            if (args.TryGetValue("spec", out var specObj))
            {
                var parsed = Deserialize(specObj, options);
                if (parsed != null) return parsed;
            }

            var json = JsonSerializer.Serialize(args, options);
            return Deserialize(json, options);
        }

        private static SpecialStructureSpec Deserialize(object input, JsonSerializerOptions options)
        {
            if (input == null) return null;
            if (input is SpecialStructureSpec ready) return ready;
            if (input is string s)
            {
                try { return SpecialStructureSpec.FromJson(s); } catch { return null; }
            }
            if (input is JsonElement element)
            {
                try { return element.Deserialize<SpecialStructureSpec>(options); } catch { return null; }
            }
            if (input is Dictionary<string, object> dict)
            {
                try
                {
                    var json = JsonSerializer.Serialize(dict, options);
                    return JsonSerializer.Deserialize<SpecialStructureSpec>(json, options);
                }
                catch { return null; }
            }
            try
            {
                var json = JsonSerializer.Serialize(input, options);
                return JsonSerializer.Deserialize<SpecialStructureSpec>(json, options);
            }
            catch
            {
                return null;
            }
        }
    }

    public class ConfigureDesignCodesSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.DesignSteel.SetCode",
            "cSapModel.DesignConcrete.SetCode",
            "cSapModel.DesignCompositeBeam.SetCode"
        };

        public string Name => "ConfigureDesignCodes";
        public string Description => "Set steel, concrete, and composite design codes so automated checks follow the desired standards.";
        public string ParamsSchema => @"{ ""steelCode"": ""AISC360-16"", ""concreteCode"": ""ACI318-19"", ""compositeBeamCode"": ""Eurocode4"" }";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            string steel = GetString(args, "steelCode", "AISC360-16");
            string concrete = GetString(args, "concreteCode", "ACI318-19");
            string composite = GetString(args, "compositeBeamCode", "AISC360-16");

            var messages = new List<string>();
            dynamic dyn = model;

            if (!string.IsNullOrWhiteSpace(steel))
            {
                try
                {
                    dyn.DesignSteel.SetCode(steel);
                    messages.Add($"Steel={steel}");
                }
                catch (Exception ex)
                {
                    messages.Add($"Steel={steel} (failed: {ex.Message})");
                }
            }

            if (!string.IsNullOrWhiteSpace(concrete))
            {
                try
                {
                    dyn.DesignConcrete.SetCode(concrete);
                    messages.Add($"Concrete={concrete}");
                }
                catch (Exception ex)
                {
                    messages.Add($"Concrete={concrete} (failed: {ex.Message})");
                }
            }

            if (!string.IsNullOrWhiteSpace(composite))
            {
                try
                {
                    dyn.DesignCompositeBeam.SetCode(composite);
                    messages.Add($"CompositeBeam={composite}");
                }
                catch (Exception ex)
                {
                    messages.Add($"CompositeBeam={composite} (failed: {ex.Message})");
                }
            }

            return messages.Count > 0 ? string.Join("; ", messages) : "No design codes updated.";
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return s;

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? defaultValue;
        }
    }

    public class SetupAdvancedAnalysesSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.LoadCases.ModalEigen.SetCase",
            "cSapModel.LoadCases.ModalEigen.SetNumberModes",
            "cSapModel.LoadCases.ResponseSpectrum.SetCase",
            "cSapModel.LoadCases.ResponseSpectrum.SetModalCase",
            "cSapModel.LoadCases.ResponseSpectrum.SetFunction",
            "cSapModel.LoadCases.TimeHistoryDirect.SetCase",
            "cSapModel.LoadCases.TimeHistoryDirect.SetMotionFunction",
            "cSapModel.LoadCases.StaticNonlinear.SetCase",
            "cSapModel.LoadCases.StaticNonlinear.SetLoadCase",
            "cSapModel.Analyze.SetRunCaseFlag"
        };

        public string Name => "SetupAdvancedAnalyses";
        public string Description => "Create modal, response spectrum, time history, and pushover cases plus optional plastic hinge defaults.";
        public string ParamsSchema => @"{
       ""modal"": { ""enabled"": true, ""caseName"": ""MODAL"", ""modes"": 12 },
       ""responseSpectrum"": { ""enabled"": true, ""caseName"": ""RS-X"", ""function"": ""UBC97"" },
        ""timeHistory"": { ""enabled"": false, ""caseName"": ""TH-X"", ""function"": ""ElCentro"" },
       ""pushover"": { ""enabled"": true, ""caseName"": ""PUSH-X"" },  
       ""assignPlasticHinges"": true  
   }";


        public IEnumerable<string> DocumentationReferences => _docRefs;


        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var responses = new List<string>();
            dynamic dyn = model;

            EnsureBasePatterns(model);

            if (TryGetToggle(args, "modal", out var modalArgs))
            {
                bool enabled = GetBool(modalArgs, "enabled", true);
                string caseName = GetString(modalArgs, "caseName", "MODAL");
                int modes = GetInt(modalArgs, "modes", 12);
                if (enabled)
                {
                    try
                    {
                        dyn.LoadCases.ModalEigen.SetCase(caseName);
                        dyn.LoadCases.ModalEigen.SetNumberModes(caseName, modes);
                        model.Analyze.SetRunCaseFlag(caseName, true);
                        responses.Add($"Modal case '{caseName}' with {modes} modes ready.");
                    }
                    catch (Exception ex)
                    {
                        responses.Add($"Modal setup failed ({ex.Message}).");
                    }
                }
            }

            if (TryGetToggle(args, "responseSpectrum", out var rsArgs))
            {
                bool enabled = GetBool(rsArgs, "enabled", true);
                if (enabled)
                {
                    string rsCase = GetString(rsArgs, "caseName", "RS-X");
                    string modalForRs = GetString(rsArgs, "modalCase", "MODAL");
                    string function = GetString(rsArgs, "function", "UBC97");
                    try
                    {
                        dyn.LoadCases.ResponseSpectrum.SetCase(rsCase);
                        dyn.LoadCases.ResponseSpectrum.SetModalCase(rsCase, modalForRs);
                        int dirX = SapApiReflection.GetEnumInt(model, "eCoorDir", "X", 1);
                        dyn.LoadCases.ResponseSpectrum.SetFunction(rsCase, function, 1.0, dirX, 0.0);
                        model.Analyze.SetRunCaseFlag(rsCase, true);
                        responses.Add($"Response spectrum case '{rsCase}' linked to modal '{modalForRs}' using '{function}'.");
                    }
                    catch (Exception ex)
                    {
                        responses.Add($"Response spectrum setup failed ({ex.Message}).");
                    }
                }
            }

            if (TryGetToggle(args, "timeHistory", out var thArgs))
            {
                bool enabled = GetBool(thArgs, "enabled", false);
                if (enabled)
                {
                    string thCase = GetString(thArgs, "caseName", "TH-X");
                    string function = GetString(thArgs, "function", "ElCentro");
                    try
                    {
                        dyn.LoadCases.TimeHistoryDirect.SetCase(thCase);
                        int dirX = SapApiReflection.GetEnumInt(model, "eCoorDir", "X", 1);
                        dyn.LoadCases.TimeHistoryDirect.SetMotionFunction(thCase, function, dirX, 1.0, 0.0);
                        model.Analyze.SetRunCaseFlag(thCase, true);
                        responses.Add($"Time history case '{thCase}' uses function '{function}'.");
                    }
                    catch (Exception ex)
                    {
                        responses.Add($"Time history setup failed ({ex.Message}).");
                    }
                }
            }

            if (TryGetToggle(args, "pushover", out var pushArgs))
            {
                bool enabled = GetBool(pushArgs, "enabled", false);
                if (enabled)
                {
                    string pushCase = GetString(pushArgs, "caseName", "PUSH-X");
                    string pattern = GetString(pushArgs, "pattern", "PUSH");
                    try
                    {
                        SapApiReflection.TryAddLoadPattern(model, pattern, "Other", 0.0, true);
                        dyn.LoadCases.StaticNonlinear.SetCase(pushCase);
                        dyn.LoadCases.StaticNonlinear.SetLoadCase(pushCase, pattern);
                        model.Analyze.SetRunCaseFlag(pushCase, true);
                        responses.Add($"Pushover case '{pushCase}' with pattern '{pattern}' created.");
                    }
                    catch (Exception ex)
                    {
                        responses.Add($"Pushover setup failed ({ex.Message}).");
                    }
                }
            }

            bool assignHinges = GetBool(args, "assignPlasticHinges", false);
            if (assignHinges)
            {
                try
                {
                    AssignDefaultPlasticHinges(model);
                    responses.Add("Plastic hinge defaults assigned to frame objects.");
                }
                catch (Exception ex)
                {
                    responses.Add($"Plastic hinge assignment failed ({ex.Message}).");
                }
            }

            return responses.Count > 0 ? string.Join("\n", responses) : "No advanced analysis changes applied.";
        }

        void EnsureBasePatterns(cSapModel model)
        {
            SapApiReflection.TryAddLoadPattern(model, "DEAD", "Dead", 1.0, true);
            SapApiReflection.TryAddLoadPattern(model, "LIVE", "Live", 0.0, true);
            SapApiReflection.TryAddLoadPattern(model, "EQX", "ResponseSpectrum", 0.0, true, "Quake", "Earthquake");
            SapApiReflection.TryAddLoadPattern(model, "EQY", "ResponseSpectrum", 0.0, true, "Quake", "Earthquake");
            SapApiReflection.TryAddLoadPattern(model, "WINDX", "Wind", 0.0, true);
            SapApiReflection.TryAddLoadPattern(model, "WINDY", "Wind", 0.0, true);
        }

        bool TryGetToggle(Dictionary<string, object> args, string key, out Dictionary<string, object> toggle)
        {
            toggle = null;
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return false;

            if (value is Dictionary<string, object> dict)
            {
                toggle = dict;
                return true;
            }

            if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    toggle = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return toggle != null;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        void AssignDefaultPlasticHinges(cSapModel model)
        {
            int number = 0;
            string[] frameNames = null;
            int ret = model.FrameObj.GetNameList(ref number, ref frameNames);
            if (ret != 0 || frameNames == null || frameNames.Length == 0)
                return;

            foreach (var frame in frameNames)
            {
                SapApiReflection.AssignPlasticHinge(model, frame, "EndI");
                SapApiReflection.AssignPlasticHinge(model, frame, "EndJ");
            }
        }

        string GetString(Dictionary<string, object> args, string key, string defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return s;

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? defaultValue;
        }

        bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is bool b)
                return b;

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
                if (element.ValueKind == JsonValueKind.String)
                {
                    if (bool.TryParse(element.GetString(), out var parsedBool))
                        return parsedBool;
                }
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var dbl))
                    return Math.Abs(dbl) > double.Epsilon;
            }

            if (value is string s)
            {
                if (bool.TryParse(s, out var parsed)) return parsed;
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dbl)) return Math.Abs(dbl) > double.Epsilon;
            }

            if (value is int i) return i != 0;
            if (value is double d) return Math.Abs(d) > double.Epsilon;

            return defaultValue;
        }

        int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is int i) return i;
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed))
                return parsed;
            if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStr))
                return parsedStr;
            if (value is double d) return (int)Math.Round(d);
            return defaultValue;
        }

    }

    internal static class SapApiReflection
    {
        public static Type GetSapType(cSapModel model, string typeName)
        {
            if (model == null || string.IsNullOrWhiteSpace(typeName))
                return null;

            var assembly = model.GetType().Assembly;
            return assembly.GetType($"SAP2000v1.{typeName}") ?? Type.GetType($"SAP2000v1.{typeName}, SAP2000v1");
        }

        public static int GetEnumInt(cSapModel model, string enumName, string valueName, int defaultValue)
        {
            var value = GetEnumValue(model, enumName, valueName);
            if (value == null)
                return defaultValue;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static object GetEnumValue(cSapModel model, string enumName, string valueName)
        {
            var enumType = GetSapType(model, enumName);
            return ResolveEnumValue(enumType, valueName);
        }

        public static void TryAddLoadPattern(cSapModel model, string name, string preferredType, double selfWeightMultiplier, bool replaceExisting, params string[] fallbackTypes)
        {
            if (model == null)
                return;

            try
            {
                var enumType = GetSapType(model, "eLoadPatternType");
                var typeValue = ResolveEnumValue(enumType, preferredType, fallbackTypes);
                if (typeValue == null)
                    return;

                var loadPatterns = model.LoadPatterns;
                var method = loadPatterns.GetType().GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 4);
                method?.Invoke(loadPatterns, new object[] { name, typeValue, selfWeightMultiplier, replaceExisting });
            }
            catch
            {
                // Ignore pattern creation failures; they may already exist or the type may not be supported.
            }
        }

        public static void AssignPlasticHinge(cSapModel model, string frameName, string locationName)
        {
            if (model == null || string.IsNullOrWhiteSpace(frameName))
                return;

            try
            {
                var frameObj = model.FrameObj;
                if (frameObj == null)
                    return;

                var method = frameObj.GetType().GetMethods().FirstOrDefault(m => m.Name == "SetHingeAssign" && m.GetParameters().Length >= 8);
                if (method == null)
                    return;

                var hingeTypeValue = ResolveEnumValue(GetSapType(model, "eHingeType"), "PMM");
                var hingeLocationValue = ResolveEnumValue(GetSapType(model, "eHingeLocation"), locationName);
                if (hingeTypeValue == null || hingeLocationValue == null)
                    return;

                var hingeTypes = Array.CreateInstance(hingeTypeValue.GetType(), 1);
                hingeTypes.SetValue(hingeTypeValue, 0);

                var locations = Array.CreateInstance(hingeLocationValue.GetType(), 1);
                locations.SetValue(hingeLocationValue, 0);

                string[] hingeNames = { "PMM" };
                double[] relDistances = { 0.0 };
                bool[] autoGenerate = { true };
                string[] existingNames = { string.Empty };

                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                args[0] = frameName;
                args[1] = 1;
                args[2] = hingeNames;
                args[3] = hingeTypes;
                args[4] = locations;
                args[5] = relDistances;
                args[6] = autoGenerate;
                args[7] = existingNames;

                for (int i = 8; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;
                }

                method.Invoke(frameObj, args);
            }
            catch
            {
                // Ignore frames that reject hinge assignment (e.g., cables or tendons)
            }
        }

        private static object ResolveEnumValue(Type enumType, string preferred, params string[] fallbacks)
        {
            if (enumType == null || !enumType.IsEnum)
                return null;

            foreach (var name in EnumerateCandidates(preferred, fallbacks))
            {
                var match = Enum.GetNames(enumType).FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    try
                    {
                        return Enum.Parse(enumType, match);
                    }
                    catch
                    {
                        // Ignore parse issues and continue to other candidates.
                    }
                }
            }

            var values = Enum.GetValues(enumType);
            return values.Length > 0 ? values.GetValue(0) : null;
        }

        private static IEnumerable<string> EnumerateCandidates(string preferred, params string[] fallbacks)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
                yield return preferred;

            if (fallbacks == null)
                yield break;

            foreach (var candidate in fallbacks)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                    yield return candidate;
            }
        }
    }

    public class SaveModelSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.File.Save"
        };

        public string Name => "SaveModel";
        public string Description => "Save the current model to path (.SDB).";
        public string ParamsSchema => @"{ ""path"": ""C:\\Temp\\MyModel.SDB"" }";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            var path = args.TryGetValue("path", out var p) ? Convert.ToString(p) : @"C:\Temp\AutoPlan.SDB";
            int ret = model.File.Save(path);
            if (ret != 0) throw new ApplicationException("File.Save failed.");
            return $"Saved model to: {path}";
        }
    }

    public class RunAnalysisSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.Analyze.SetRunCaseFlag",
            "cSapModel.Analyze.DeleteResults",
            "cSapModel.Analyze.RunAnalysis"
        };

        public string Name => "RunAnalysis";
        public string Description => "Run analysis.";
        public string ParamsSchema => @"{ }";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            // Make sure at least one reasonable case is active before running the solver.
            model.Analyze.SetRunCaseFlag("HYDROSTATIC", true);
            model.Analyze.SetRunCaseFlag("DEAD", true);

            // Clear any stale results so the analysis can start cleanly.
            model.Analyze.DeleteResults("", true);

            int ret = model.Analyze.RunAnalysis();
            if (ret != 0)
                throw new ApplicationException($"RunAnalysis failed (code {ret}). Ensure that at least one load case is enabled.");
            return "Analysis completed.";
        }
    }

    public class GetModelInfoSkill : ISkill
    {
        private static readonly string[] _docRefs = new[]
        {
            "cSapModel.GetModelFilename",
            "cSapModel.PointObj.GetNameList"
        };

        public string Name => "GetModelInfo";
        public string Description => "Read model filename and joint count.";
        public string ParamsSchema => @"{ ""includePath"": true }";
        public IEnumerable<string> DocumentationReferences => _docRefs;

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            bool includePath = args.TryGetValue("includePath", out var v) && Convert.ToBoolean(v);
            string filename = _GetModelFilename(model, includePath);

            int numberNames = 0;
            string[] jointNames = null;
            int ret = model.PointObj.GetNameList(ref numberNames, ref jointNames);
            if (ret != 0) throw new ApplicationException("GetNameList failed.");

            return $"File: {filename} | JointCount: {numberNames}";
        }

        private static string _GetModelFilename(cSapModel model, bool includePath)
        {
            // Your build exposes: string GetModelFilename(bool IncludePath = true)
            return model.GetModelFilename(includePath);
        }
    }
}

