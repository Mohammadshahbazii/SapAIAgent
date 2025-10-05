using System;
using System.Collections.Generic;
using System.Linq;
using SAP2000v1;

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

                    string p1 = AddOrGetPoint(model,  coreHalf,  coreHalf, zBot);
                    string p2 = AddOrGetPoint(model, -coreHalf,  coreHalf, zBot);
                    string p3 = AddOrGetPoint(model, -coreHalf, -coreHalf, zTop);
                    string p4 = AddOrGetPoint(model,  coreHalf, -coreHalf, zTop);

                    wallPoints[0] = p1;
                    wallPoints[1] = p2;
                    wallPoints[2] = p3;
                    wallPoints[3] = p4;

                    string wallName = "";
                    int wallRet = model.AreaObj.AddByPoint(ref wallPoints, ref wallName, shearWallSection);
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

                        string areaName = "";
                        int ret = model.AreaObj.AddByPoint(ref floorPts, ref areaName, deckProperty);
                        if (ret != 0) throw new ApplicationException("Failed to create deck panel.");
                        result.deckPanels++;
                    }
                }
            }

            model.Analyze.SetRunCaseFlag("DEAD", true);
            model.Analyze.SetRunCaseFlag("MODAL", true);

            return result;
        }

        private static string AddOrGetPoint(cSapModel model, double x, double y, double z)
        {
            string existing = string.Empty;
            int ret = model.PointObj.GetClosestPoint(x, y, z, ref existing);
            if (ret == 0 && !string.IsNullOrEmpty(existing))
                return existing;

            string created = "";
            int createRet = model.PointObj.AddCartesian(x, y, z, ref created);
            if (createRet != 0) throw new ApplicationException("Failed to create auxiliary point.");
            return created;
        }

        private static void EnsureShellSection(cSapModel model, string propertyName, ShellSectionDefinition def, string defaultMaterial)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                propertyName = def?.name ?? "ShellProperty";

            string mat = def?.material ?? defaultMaterial ?? "Concrete4000";
            double thickness = def?.thickness ?? 0.2;

            int ret = model.PropArea.SetShell(propertyName, eShellType.ShellThin, mat, thickness, 0, 0, 0, 0);
            if (ret != 0) throw new ApplicationException($"Failed to define shell property '{propertyName}'.");
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
