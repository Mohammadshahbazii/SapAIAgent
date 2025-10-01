
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using SAP2000v1;

namespace Sap2000WinFormsSample
{
    public interface ISkill
    {
        string Name { get; } // action name as used in plan
        string Description { get; } // brief description for the LLM
        string ParamsSchema { get; } // JSON-like example for guidance
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
        public string Name => "InitializeBlankModel";
        public string Description => "Create a new blank model in a specified unit system.";
        public string ParamsSchema => @"{ ""units"": ""kN_m_C|N_mm_C|kip_ft_F|kip_in_F"" }";

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
        public string Name => "SetUnits";
        public string Description => "Set present units for subsequent API calls.";
        public string ParamsSchema => @"{ ""units"": ""kN_m_C|N_mm_C|kip_ft_F|kip_in_F"" }";

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
        public string Name => "BuildCylindricalReservoir";
        public string Description => "Create cylindrical reservoir frames (rings + verticals).";
        public string ParamsSchema => @"{
  ""units"": ""kN_m_C"",
  ""diameter"": 10.0, ""height"": 8.0,
  ""shellThickness"": 0.2,
  ""numWallSegments"": 24, ""numHeightSegments"": 8,
  ""foundationElevation"": 0.0,
  ""liquidHeight"": 6.0,
  ""unitWeight"": 9.81,
  ""fixBase"": true
}";

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
            bool fixBase = GetBool(args, "fixBase", true);

            if (args.TryGetValue("geometry", out var geometryObj) && geometryObj != null)
            {
                if (geometryObj is Dictionary<string, object> geometryDict)
                {
                    D = GetD(geometryDict, "diameter", D);
                    H = GetD(geometryDict, "height", H);
                    shellThickness = GetD(geometryDict, "shellThickness", shellThickness);
                    nCirc = (int)GetD(geometryDict, "numWallSegments", nCirc);
                    nZ = (int)GetD(geometryDict, "numHeightSegments", nZ);
                }
                else if (geometryObj is JsonElement geometryElement && geometryElement.ValueKind == JsonValueKind.Object)
                {
                    if (geometryElement.TryGetProperty("diameter", out var v)) D = GetD(v, D);
                    if (geometryElement.TryGetProperty("height", out var v)) H = GetD(v, H);
                    if (geometryElement.TryGetProperty("shellThickness", out var v)) shellThickness = GetD(v, shellThickness);
                    if (geometryElement.TryGetProperty("numWallSegments", out var v)) nCirc = (int)GetD(v, nCirc);
                    if (geometryElement.TryGetProperty("numHeightSegments", out var v)) nZ = (int)GetD(v, nZ);
                }
            }

            if (args.TryGetValue("loads", out var loadsObj) && loadsObj != null)
            {
                if (loadsObj is Dictionary<string, object> loadDict)
                {
                    liquidHeight = GetD(loadDict, "liquidHeight", liquidHeight);
                    unitWeight = GetD(loadDict, "unitWeight", unitWeight);
                }
                else if (loadsObj is JsonElement loadElement && loadElement.ValueKind == JsonValueKind.Object)
                {
                    if (loadElement.TryGetProperty("liquidHeight", out var v)) liquidHeight = GetD(v, liquidHeight);
                    if (loadElement.TryGetProperty("unitWeight", out var v)) unitWeight = GetD(v, unitWeight);
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
            var spec = new TankSpec
            {
                units = specUnits,
                geometry = new Geometry
                {
                    diameter = D,
                    height = H,
                    numWallSegments = nCirc,
                    numHeightSegments = nZ,
                    shellThickness = shellThickness
                },
                loads = (liquidHeight > 0 && unitWeight > 0)
                    ? new Loads { liquidHeight = liquidHeight, unitWeight = unitWeight }
                    : null,
                foundationElevation = z0,
                fixBase = fixBase
            };

            int count = SapBuilder.BuildCylindricalReservoirFrames(model, spec);
            return $"Cylindrical reservoir frames created: {count}";
        }

        private static double GetD(Dictionary<string, object> d, string k, double defVal)
        {
            if (!d.TryGetValue(k, out var v) || v == null) return defVal;
            if (v is JsonElement je) return GetD(je, defVal);
            if (v is double dd) return dd;
            if (v is float ff) return ff;
            if (v is int ii) return ii;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return defVal;
        }

        private static double GetD(JsonElement element, double defVal)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetDouble(out var val)) return val;
                    break;
                case JsonValueKind.String:
                    var str = element.GetString();
                    if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                    break;
            }
            return defVal;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool defaultValue)
        {
            if (!d.TryGetValue(key, out var v) || v == null) return defaultValue;
            if (v is JsonElement je) return GetBool(je, defaultValue);
            if (v is bool b) return b;
            if (v is int i) return i != 0;
            if (v is double dbl) return Math.Abs(dbl) > double.Epsilon;
            if (v is string s)
            {
                if (bool.TryParse(s, out var parsedBool)) return parsedBool;
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt)) return parsedInt != 0;
            }
            return defaultValue;
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

        private static eUnits? TryResolveUnits(object unitsObj, ref Units specUnits)
        {
            if (unitsObj == null) return null;

            if (unitsObj is eUnits direct)
            {
                UpdateUnitsForSpec(direct, ref specUnits);
                return direct;
            }

            if (unitsObj is string s)
                return ResolveUnitsFromCode(s, ref specUnits);

            if (unitsObj is Dictionary<string, object> dict)
            {
                string length = AsString(dict.TryGetValue("length", out var len) ? len : null);
                string force = AsString(dict.TryGetValue("force", out var frc) ? frc : null);
                var fromComponents = ResolveUnitsFromComponents(length, force, ref specUnits);
                if (fromComponents.HasValue) return fromComponents;
            }

            if (unitsObj is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    return ResolveUnitsFromCode(element.GetString(), ref specUnits);

                if (element.ValueKind == JsonValueKind.Object)
                {
                    string length = element.TryGetProperty("length", out var lenEl) ? lenEl.GetString() : null;
                    string force = element.TryGetProperty("force", out var forceEl) ? forceEl.GetString() : null;
                    var fromComponents = ResolveUnitsFromComponents(length, force, ref specUnits);
                    if (fromComponents.HasValue) return fromComponents;
                }
            }

            return ResolveUnitsFromCode(Convert.ToString(unitsObj, CultureInfo.InvariantCulture), ref specUnits);
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

    public class SaveModelSkill : ISkill
    {
        public string Name => "SaveModel";
        public string Description => "Save the current model to path (.SDB).";
        public string ParamsSchema => @"{ ""path"": ""C:\\Temp\\MyModel.SDB"" }";

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
        public string Name => "RunAnalysis";
        public string Description => "Run analysis.";
        public string ParamsSchema => @"{ }";

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            int ret = model.Analyze.RunAnalysis();
            if (ret != 0) throw new ApplicationException("RunAnalysis failed.");
            return "Analysis completed.";
        }
    }

    public class GetModelInfoSkill : ISkill
    {
        public string Name => "GetModelInfo";
        public string Description => "Read model filename and joint count.";
        public string ParamsSchema => @"{ ""includePath"": true }";

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
