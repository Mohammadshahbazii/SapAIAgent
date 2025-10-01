
using System;
using System.Collections.Generic;
using System.Globalization;
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
  ""numWallSegments"": 24, ""numHeightSegments"": 8,
  ""foundationElevation"": 0.0
}";

        public string Execute(cSapModel model, Dictionary<string, object> args)
        {
            double D = GetD(args, "diameter", 10);
            double H = GetD(args, "height", 8);
            int nCirc = (int)GetD(args, "numWallSegments", 24);
            int nZ = (int)GetD(args, "numHeightSegments", 8);
            double z0 = GetD(args, "foundationElevation", 0);

            // Ensure units if provided
            if (args.TryGetValue("units", out var unitsObj))
            {
                var unitsStr = Convert.ToString(unitsObj, CultureInfo.InvariantCulture);
                var units = unitsStr == "N_mm_C" ? eUnits.N_mm_C :
                            unitsStr == "kip_ft_F" ? eUnits.kip_ft_F :
                            unitsStr == "kip_in_F" ? eUnits.kip_in_F :
                            eUnits.kN_m_C;
                int retU = model.SetPresentUnits(units);
                if (retU != 0) throw new ApplicationException("SetPresentUnits in builder failed.");
            }

            // Build via the helper you already have
            var spec = new TankSpec
            {
                units = new Units { length = "m", force = "kN" },
                geometry = new Geometry
                {
                    diameter = D,
                    height = H,
                    numWallSegments = nCirc,
                    numHeightSegments = nZ
                },
                foundationElevation = z0
            };

            int count = SapBuilder.BuildCylindricalReservoirFrames(model, spec);
            return $"Cylindrical reservoir frames created: {count}";
        }

        private static double GetD(Dictionary<string, object> d, string k, double defVal)
        {
            if (!d.TryGetValue(k, out var v) || v == null) return defVal;
            if (v is double dd) return dd;
            if (v is float ff) return ff;
            if (v is int ii) return ii;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return defVal;
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
