using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SAP2000v1;
using Microsoft.CSharp.RuntimeBinder;

namespace Sap2000WinFormsSample
{
    public static class SapBuilder
    {
        /// <summary>
        /// Creates a cylindrical frame “mesh” (rings + verticals) centered at (0,0,foundationZ).
        /// Returns total created members.
        /// </summary>
        public static int BuildCylindricalReservoirFrames(cSapModel model, TankSpec spec)
        {
            if (model == null || spec == null) throw new ArgumentNullException();

            // Units (very basic mapping)
            eUnits units = eUnits.kN_m_C;
            if (spec.units != null)
            {
                if (spec.units.length?.ToLowerInvariant() == "mm" && (spec.units.force?.ToLowerInvariant() == "n"))
                    units = eUnits.N_mm_C;
                else if (spec.units.length?.ToLowerInvariant() == "in")
                    units = eUnits.kip_in_F;
                else if (spec.units.length?.ToLowerInvariant() == "ft")
                    units = eUnits.kip_ft_F;
            }
            int ret = model.SetPresentUnits(units);
            if (ret != 0) throw new ApplicationException("SetPresentUnits failed.");

            double D = spec.geometry.diameter;
            double H = spec.geometry.height;
            int nCirc = Math.Max(12, spec.geometry.numWallSegments);
            int nZ = Math.Max(2, spec.geometry.numHeightSegments);

            double R = D / 2.0;
            double z0 = spec.foundationElevation;
            double dz = H / (nZ - 1);
            double dTheta = 2.0 * Math.PI / nCirc;
            double panelWidth = 2.0 * Math.PI * R / nCirc;

            bool applyHydro = spec.loads != null && spec.loads.liquidHeight > 0 && spec.loads.unitWeight > 0;
            double liquidHeight = applyHydro ? spec.loads.liquidHeight : 0.0;
            double unitWeight = applyHydro ? spec.loads.unitWeight : 0.0;

            var jointLoads = applyHydro ? new Dictionary<string, (double Fx, double Fy)>(StringComparer.OrdinalIgnoreCase) : null;

            // Create all ring points
            string[,] pNames = new string[nZ, nCirc];
            int created = 0;

            for (int iz = 0; iz < nZ; iz++)
            {
                double z = z0 + iz * dz;
                for (int ic = 0; ic < nCirc; ic++)
                {
                    double theta = ic * dTheta;
                    double x = R * Math.Cos(theta);
                    double y = R * Math.Sin(theta);

                    string pName = "";
                    // PointObj.AddCartesian(double X, double Y, double Z, ref string Name, string UserName="", string CSys="Global")
                    ret = model.PointObj.AddCartesian(x, y, z, ref pName);
                    if (ret != 0) throw new ApplicationException("Point add failed.");
                    pNames[iz, ic] = pName;

                }
            }

            // Restrain base ring if requested
            if (spec.fixBase)
            {
                for (int ic = 0; ic < nCirc; ic++)
                {
                    var restraints = new bool[] { true, true, true, true, true, true };
                    ret = model.PointObj.SetRestraint(pNames[0, ic], ref restraints);
                    if (ret != 0) throw new ApplicationException("Failed to set base restraint.");
                }
            }

            // Create vertical frames
            for (int ic = 0; ic < nCirc; ic++)
            {
                for (int iz = 0; iz < nZ - 1; iz++)
                {
                    string name = "";
                    ret = model.FrameObj.AddByPoint(pNames[iz, ic], pNames[iz + 1, ic], ref name);
                    if (ret != 0) throw new ApplicationException("Frame add failed (vertical).");
                    created++;

                    if (applyHydro)
                    {
                        double segmentTop = iz * dz;
                        double segmentBottom = (iz + 1) * dz;

                        double submergedHeight = Math.Max(0.0, Math.Min(segmentBottom, liquidHeight) - Math.Min(segmentTop, liquidHeight));
                        if (submergedHeight <= 0)
                            continue;

                        double depthTop = Math.Max(0.0, liquidHeight - segmentTop);
                        double depthBottom = Math.Max(0.0, liquidHeight - segmentBottom);
                        double avgPressure = 0.5 * (depthTop + depthBottom) * unitWeight;
                        double panelForce = avgPressure * panelWidth * submergedHeight;
                        if (panelForce <= 0)
                            continue;

                        double theta = ic * dTheta;
                        double ux = Math.Cos(theta);
                        double uy = Math.Sin(theta);
                        double halfForceX = 0.5 * panelForce * ux;
                        double halfForceY = 0.5 * panelForce * uy;

                        string topPoint = pNames[iz, ic];
                        string bottomPoint = pNames[iz + 1, ic];

                        if (jointLoads != null)
                        {
                            if (!jointLoads.TryGetValue(topPoint, out var topLoad))
                                topLoad = (0.0, 0.0);
                            jointLoads[topPoint] = (topLoad.Fx + halfForceX, topLoad.Fy + halfForceY);

                            if (!jointLoads.TryGetValue(bottomPoint, out var bottomLoad))
                                bottomLoad = (0.0, 0.0);
                            jointLoads[bottomPoint] = (bottomLoad.Fx + halfForceX, bottomLoad.Fy + halfForceY);
                        }
                    }
                }
            }

            // Create ring frames
            for (int iz = 0; iz < nZ; iz++)
            {
                for (int ic = 0; ic < nCirc; ic++)
                {
                    int ic2 = (ic + 1) % nCirc;
                    string name = "";
                    ret = model.FrameObj.AddByPoint(pNames[iz, ic], pNames[iz, ic2], ref name);
                    if (ret != 0) throw new ApplicationException("Frame add failed (ring).");
                    created++;
                }
            }

            if (applyHydro)
            {
                const string hydroPattern = "HYDROSTATIC";
                // Try to add the load pattern; ignore if it already exists
                int retAdd = model.LoadPatterns.Add(hydroPattern, eLoadPatternType.Other, 0, true);
                if (retAdd != 0)
                {
                    // The pattern might already exist; try to continue by clearing retAdd
                    retAdd = 0;
                }

                foreach (var kvp in jointLoads)
                {
                    double fx = kvp.Value.Fx;
                    double fy = kvp.Value.Fy;
                    if (Math.Abs(fx) < 1e-6 && Math.Abs(fy) < 1e-6)
                        continue;

                    var forces = new double[] { fx, fy, 0, 0, 0, 0 };
                    int retLoad = model.PointObj.SetLoadForce(kvp.Key, hydroPattern, ref forces, false);
                    if (retLoad != 0) throw new ApplicationException($"Failed to assign hydrostatic load at joint {kvp.Key}.");
                }

                // Ensure the newly created hydrostatic case is flagged to run. SAP2000 creates the
                // case automatically when we pass addCase = true above, but the run flag is left
                // disabled in a blank model. Without setting the flag the subsequent analysis call
                // returns a non-zero error code.
                int retRunFlag = model.Analyze.SetRunCaseFlag(hydroPattern, true);
                if (retRunFlag != 0)
                {
                    // Fall back to enabling the default dead-load case so that at least one case
                    // is active for analysis.
                    model.Analyze.SetRunCaseFlag("DEAD", true);
                }
            }

            // Mark the default dead-load case to run as a final safety net. New blank models ship
            // with the case defined but disabled, which causes Analyze.RunAnalysis to fail.
            model.Analyze.SetRunCaseFlag("DEAD", true);

            return created;
        }

        public class BuildingBuildResult
        {
            public int jointCount;
            public int frameCount;
            public int beamCount;
            public int braceCount;
            public int shearWallPanels;
            public int deckPanels;
        }

        public static BuildingBuildResult BuildMultiStoryBuilding(cSapModel model, BuildingDesignSpec spec)
        {
            if (model == null || spec == null) throw new ArgumentNullException();

            var layout = spec.layout ?? new BuildingLayout();
            int baysX = Math.Max(1, layout.baysX);
            int baysY = Math.Max(1, layout.baysY);
            double spacingX = layout.baySpacingX > 0 ? layout.baySpacingX : 6.0;
            double spacingY = layout.baySpacingY > 0 ? layout.baySpacingY : 6.0;

            var stories = (spec.stories != null && spec.stories.Count > 0)
                ? spec.stories
                : Enumerable.Range(0, 5).Select(i => new StorySpec
                {
                    name = $"Story {i + 1}",
                    height = 3.2
                }).ToList();

            var units = ResolveUnits(spec.units);
            int retUnits = model.SetPresentUnits(units);
            if (retUnits != 0) throw new ApplicationException("Failed to set present units for building generation.");

            double totalWidthX = baysX * spacingX;
            double totalWidthY = baysY * spacingY;
            double originX = -totalWidthX / 2.0;
            double originY = -totalWidthY / 2.0;

            int nodesX = baysX + 1;
            int nodesY = baysY + 1;
            int levels = stories.Count + 1;

            var jointNames = new string[levels, nodesX, nodesY];
            double currentElevation = layout.baseElevation;

            var elevations = new double[levels];
            elevations[0] = currentElevation;
            for (int i = 0; i < stories.Count; i++)
            {
                currentElevation += Math.Max(0.5, stories[i].height);
                elevations[i + 1] = currentElevation;
            }

            var pointCache = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int level = 0; level < levels; level++)
            {
                double z = elevations[level];
                for (int ix = 0; ix < nodesX; ix++)
                {
                    double x = originX + ix * spacingX;
                    for (int iy = 0; iy < nodesY; iy++)
                    {
                        double y = originY + iy * spacingY;
                        string name = "";
                        int ret = model.PointObj.AddCartesian(x, y, z, ref name);
                        if (ret != 0) throw new ApplicationException("Failed to create joint during building generation.");
                        jointNames[level, ix, iy] = name;
                        pointCache[MakePointKey(x, y, z)] = name;
                    }
                }
            }

            var ensuredFrameSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var materials = spec.materials ?? new BuildingMaterials();

            void EnsureFrameSection(string sectionName, FrameSectionDefinition def, string defaultMaterial)
            {
                if (string.IsNullOrWhiteSpace(sectionName) || ensuredFrameSections.Contains(sectionName))
                    return;

                string mat = def?.material ?? defaultMaterial ?? materials.steelMaterial;
                double depth = def?.depth ?? 0.4;
                double width = def?.width ?? 0.3;

                int ret = model.PropFrame.SetRectangle(sectionName, mat, depth, width);
                if (ret != 0) throw new ApplicationException($"Failed to define frame section '{sectionName}'.");
                ensuredFrameSections.Add(sectionName);
            }

            string DetermineColumnSection(StorySpec story)
            {
                if (!string.IsNullOrWhiteSpace(story.columnSection))
                    return story.columnSection;

                if (string.Equals(materials.concreteMaterial, "", StringComparison.Ordinal))
                    return materials.steelColumn?.name ?? "SteelColumn300x300";

                return materials.concreteColumn?.name ?? materials.steelColumn?.name ?? "Column300x300";
            }

            string DetermineBeamSection(StorySpec story)
            {
                if (!string.IsNullOrWhiteSpace(story.beamSection))
                    return story.beamSection;

                return materials.steelBeam?.name ?? materials.concreteBeam?.name ?? "Beam250x400";
            }

            string DetermineBraceSection(StorySpec story)
            {
                if (!string.IsNullOrWhiteSpace(story.braceSection))
                    return story.braceSection;
                return materials.brace?.name ?? DetermineBeamSection(story);
            }

            foreach (var story in stories)
            {
                var columnSection = DetermineColumnSection(story);
                var beamSection = DetermineBeamSection(story);
                var braceSection = DetermineBraceSection(story);

                EnsureFrameSection(columnSection, materials.steelColumn ?? materials.concreteColumn, materials.steelMaterial);
                EnsureFrameSection(beamSection, materials.steelBeam ?? materials.concreteBeam, materials.steelMaterial);
                EnsureFrameSection(braceSection, materials.brace, materials.steelMaterial);
            }

            var result = new BuildingBuildResult();
            result.jointCount = jointNames.Length;

            for (int level = 0; level < levels - 1; level++)
            {
                var story = stories[level];
                string columnSection = DetermineColumnSection(story);

                for (int ix = 0; ix < nodesX; ix++)
                {
                    for (int iy = 0; iy < nodesY; iy++)
                    {
                        string bottom = jointNames[level, ix, iy];
                        string top = jointNames[level + 1, ix, iy];
                        string frameName = "";
                        int ret = model.FrameObj.AddByPoint(bottom, top, ref frameName, columnSection);
                        if (ret != 0) throw new ApplicationException("Failed to create column frame.");
                        result.frameCount++;
                    }
                }
            }

            for (int level = 1; level < levels; level++)
            {
                var story = stories[level - 1];
                string beamSection = DetermineBeamSection(story);

                for (int iy = 0; iy < nodesY; iy++)
                {
                    for (int ix = 0; ix < nodesX - 1; ix++)
                    {
                        string start = jointNames[level, ix, iy];
                        string end = jointNames[level, ix + 1, iy];
                        string frameName = "";
                        int ret = model.FrameObj.AddByPoint(start, end, ref frameName, beamSection);
                        if (ret != 0) throw new ApplicationException("Failed to create beam (X-direction).");
                        result.beamCount++;
                    }
                }

                for (int ix = 0; ix < nodesX; ix++)
                {
                    for (int iy = 0; iy < nodesY - 1; iy++)
                    {
                        string start = jointNames[level, ix, iy];
                        string end = jointNames[level, ix, iy + 1];
                        string frameName = "";
                        int ret = model.FrameObj.AddByPoint(start, end, ref frameName, beamSection);
                        if (ret != 0) throw new ApplicationException("Failed to create beam (Y-direction).");
                        result.beamCount++;
                    }
                }
            }

            var lateral = spec.lateralSystem ?? new LateralSystemSpec();
            bool wantsBraces = string.Equals(lateral.systemType, "BracedFrame", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lateral.systemType, "Dual", StringComparison.OrdinalIgnoreCase)
                || lateral.addBracesInBothDirections;

            if (wantsBraces)
            {
                for (int level = 1; level < levels; level++)
                {
                    var story = stories[level - 1];
                    string braceSection = DetermineBraceSection(story);

                    for (int ix = 0; ix < nodesX - 1; ix++)
                    {
                        for (int iy = 0; iy < nodesY - 1; iy++)
                        {
                            string n1 = jointNames[level - 1, ix, iy];
                            string n2 = jointNames[level, ix + 1, iy + 1];
                            string brace1 = "";
                            int ret1 = model.FrameObj.AddByPoint(n1, n2, ref brace1, braceSection);
                            if (ret1 == 0) result.braceCount++;

                            string n3 = jointNames[level - 1, ix + 1, iy];
                            string n4 = jointNames[level, ix, iy + 1];
                            string brace2 = "";
                            int ret2 = model.FrameObj.AddByPoint(n3, n4, ref brace2, braceSection);
                            if (ret2 == 0) result.braceCount++;
                        }
                    }
                }
            }

            bool wantsShearWalls = string.Equals(lateral.systemType, "ShearWall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lateral.systemType, "Dual", StringComparison.OrdinalIgnoreCase)
                || lateral.addShearWallCore;

            string shearWallSection = materials.shearWall?.name ?? "ShearWallCore";
            if (wantsShearWalls)
            {
                EnsureShellSection(model, shearWallSection, materials.shearWall, materials.concreteMaterial);

                double coreHalf = Math.Max(spacingX, spacingY) * 0.25;
                if (lateral.shearWallCoreSize > 0)
                    coreHalf = lateral.shearWallCoreSize / 2.0;

                for (int level = 0; level < levels - 1; level++)
                {
                    string[] wallPoints = new string[4];
                    double zBot = elevations[level];
                    double zTop = elevations[level + 1];

                    string p1 = AddOrGetPoint(model, pointCache,  coreHalf,  coreHalf, zBot);
                    string p2 = AddOrGetPoint(model, pointCache, -coreHalf,  coreHalf, zBot);
                    string p3 = AddOrGetPoint(model, pointCache, -coreHalf, -coreHalf, zTop);
                    string p4 = AddOrGetPoint(model, pointCache,  coreHalf, -coreHalf, zTop);

                    wallPoints[0] = p1;
                    wallPoints[1] = p2;
                    wallPoints[2] = p3;
                    wallPoints[3] = p4;

                    string wallName;
                    int wallRet = AddAreaByPoint(model, wallPoints, shearWallSection, out wallName);
                    if (wallRet != 0) throw new ApplicationException("Failed to create shear wall panel.");
                    result.shearWallPanels++;
                }
            }

            var deck = spec.deck ?? new DeckSpec();
            string deckProperty = deck.propertyName ?? "CompositeDeck";
            EnsureShellSection(model, deckProperty, materials.slab ?? new ShellSectionDefinition
            {
                name = deckProperty,
                material = deck.material ?? materials.concreteMaterial,
                thickness = deck.thickness
            }, deck.material ?? materials.concreteMaterial);

            for (int level = 1; level < levels; level++)
            {
                for (int ix = 0; ix < nodesX - 1; ix++)
                {
                    for (int iy = 0; iy < nodesY - 1; iy++)
                    {
                        string[] floorPts =
                        {
                            jointNames[level, ix, iy],
                            jointNames[level, ix + 1, iy],
                            jointNames[level, ix + 1, iy + 1],
                            jointNames[level, ix, iy + 1]
                        };

                        string areaName;
                        int ret = AddAreaByPoint(model, floorPts, deckProperty, out areaName);
                        if (ret != 0) throw new ApplicationException("Failed to create deck panel.");
                        result.deckPanels++;
                    }
                }
            }

            model.Analyze.SetRunCaseFlag("DEAD", true);
            model.Analyze.SetRunCaseFlag("MODAL", true);

            return result;
        }

        private static string AddOrGetPoint(cSapModel model, Dictionary<string, string> cache, double x, double y, double z)
        {
            string key = MakePointKey(x, y, z);
            if (cache != null && cache.TryGetValue(key, out var cached))
                return cached;

            string created = "";
            int createRet = model.PointObj.AddCartesian(x, y, z, ref created);
            if (createRet != 0) throw new ApplicationException("Failed to create auxiliary point.");
            cache[key] = created;
            return created;
        }

        private static void EnsureShellSection(cSapModel model, string propertyName, ShellSectionDefinition def, string defaultMaterial)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                propertyName = def?.name ?? "ShellProperty";

            string mat = def?.material ?? defaultMaterial ?? "Concrete4000";
            double thickness = def?.thickness ?? 0.2;

            object propArea = model?.PropArea;
            if (propArea == null)
                throw new ApplicationException("SAP2000 PropArea interface is unavailable.");

            object shellType = ResolveShellThin(model);
            int ret = SetShellProperty(propArea, propertyName, shellType, mat, thickness);
            if (ret != 0) throw new ApplicationException($"Failed to define shell property '{propertyName}'.");
        }

        private static object ResolveShellThin(cSapModel model)
        {
            var assembly = model?.GetType()?.Assembly;
            var enumType = assembly?.GetType("SAP2000v1.eShellType");
            if (enumType != null && Enum.IsDefined(enumType, "ShellThin"))
                return Enum.Parse(enumType, "ShellThin");

            // Fallback to integer constant used by older SAP2000 builds: ShellThin = 1.
            return 1;
        }

        private const int ShellThinValue = 1;

        private static int SetShellProperty(object propArea, string propertyName, object shellType, string material, double thickness)
        {
            if (propArea == null) throw new ArgumentNullException(nameof(propArea));

            int shellValue = ConvertShellTypeValue(shellType);
            double membrane = 0.0;
            double bending = 0.0;
            double shear = 0.0;
            double thermal = 0.0;

            // Attempt a dynamic dispatch first. Some versions of the SAP2000 COM
            // libraries expose the SetShell method only through the dispatch
            // interface, and older builds suffix the method name with "_1". Using
            // dynamic keeps the late-bound invocation logic simple while still
            // allowing us to fall back to the more exhaustive reflection-based
            // search below when needed.
            try
            {
                dynamic area = propArea;
                return area.SetShell(propertyName, shellValue, material, thickness, membrane, bending, shear, thermal);
            }
            catch (RuntimeBinderException)
            {
                // Continue to the other strategies.
            }

            try
            {
                dynamic area = propArea;
                return area.SetShell_1(propertyName, shellValue, material, thickness, membrane, bending, shear, thermal);
            }
            catch (RuntimeBinderException)
            {
                // Continue to the reflection-based search below.
            }

            var args = new object[]
            {
                propertyName,
                shellValue,
                material,
                thickness,
                membrane,
                bending,
                shear,
                thermal
            };

            return InvokeComMethod(propArea, "SetShell", args);
        }

        private static int ConvertShellTypeValue(object shellType)
        {
            if (shellType == null)
                return ShellThinValue;

            var type = shellType.GetType();
            if (type.IsEnum)
                return Convert.ToInt32(shellType, CultureInfo.InvariantCulture);

            if (shellType is IConvertible convertible)
                return convertible.ToInt32(CultureInfo.InvariantCulture);

            return ShellThinValue;
        }

        private static int InvokeComMethod(object comObject, string methodName, object[] args)
        {
            if (comObject == null) throw new ArgumentNullException(nameof(comObject));

            const BindingFlags flags = BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public;
            Type comType = comObject.GetType();
            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in EnumerateCandidateMethodNames(comType, methodName))
            {
                if (!tried.Add(candidate))
                    continue;

                try
                {
                    object result = comType.InvokeMember(candidate, flags, binder: null, target: comObject, args: args);
                    if (result == null)
                        return 0;
                    return Convert.ToInt32(result, CultureInfo.InvariantCulture);
                }
                catch (MissingMethodException)
                {
                    continue;
                }
                catch (TargetInvocationException tie) when (tie.InnerException is MissingMethodException)
                {
                    continue;
                }
            }

            throw new MissingMethodException($"Method '{methodName}' not found on type '{comType?.FullName}'.");
        }

        private static IEnumerable<string> EnumerateCandidateMethodNames(Type comType, string baseName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(baseName))
            {
                if (seen.Add(baseName))
                    yield return baseName;

                for (int i = 1; i <= 4; i++)
                {
                    string suffixed = baseName + "_" + i.ToString(CultureInfo.InvariantCulture);
                    if (seen.Add(suffixed))
                        yield return suffixed;
                }
            }

            if (comType == null)
                yield break;

            var additional = comType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(m => m.Name)
                .Where(name => !string.IsNullOrEmpty(baseName) && name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            foreach (var name in additional)
            {
                if (seen.Add(name))
                    yield return name;
            }
        }

        private static int AddAreaByPoint(cSapModel model, string[] pointNames, string property, out string createdName)
        {
            if (pointNames == null) throw new ArgumentNullException(nameof(pointNames));

            dynamic areaObj = model.AreaObj;
            createdName = string.Empty;
            string propertyValue = property ?? string.Empty;

            try
            {
                string name = string.Empty;
                int ret = areaObj.AddByPoint(pointNames, ref name, propertyValue);
                createdName = name ?? string.Empty;
                return ret;
            }
            catch (RuntimeBinderException)
            {
                try
                {
                    string[] names = Array.Empty<string>();
                    int ret = areaObj.AddByPoint(pointNames, ref names, propertyValue);
                    createdName = (names != null && names.Length > 0) ? names[0] : string.Empty;
                    return ret;
                }
                catch (RuntimeBinderException)
                {
                    try
                    {
                        string[] names = Array.Empty<string>();
                        string propCopy = propertyValue;
                        int ret = areaObj.AddByPoint(pointNames, ref names, ref propCopy);
                        createdName = (names != null && names.Length > 0) ? names[0] : string.Empty;
                        return ret;
                    }
                    catch (RuntimeBinderException)
                    {
                        string name = string.Empty;
                        string propCopy = propertyValue;
                        int ret = areaObj.AddByPoint(pointNames, ref name, ref propCopy);
                        createdName = name ?? string.Empty;
                        return ret;
                    }
                }
            }
        }

        private static string MakePointKey(double x, double y, double z)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F6}|{1:F6}|{2:F6}", Math.Round(x, 6), Math.Round(y, 6), Math.Round(z, 6));
        }

        private static eUnits ResolveUnits(Units units)
        {
            if (units == null)
                return eUnits.kN_m_C;

            if (string.Equals(units.length, "mm", StringComparison.OrdinalIgnoreCase) && string.Equals(units.force, "N", StringComparison.OrdinalIgnoreCase))
                return eUnits.N_mm_C;

            if (string.Equals(units.length, "ft", StringComparison.OrdinalIgnoreCase) && string.Equals(units.force, "kip", StringComparison.OrdinalIgnoreCase))
                return eUnits.kip_ft_F;

            if (string.Equals(units.length, "in", StringComparison.OrdinalIgnoreCase) && string.Equals(units.force, "kip", StringComparison.OrdinalIgnoreCase))
                return eUnits.kip_in_F;

            return eUnits.kN_m_C;
        }
    }
}
