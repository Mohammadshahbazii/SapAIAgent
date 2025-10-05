using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

        public class IndustrialBuildResult
        {
            public int jointCount;
            public int frameMembers;
            public int roofMembers;
            public int braceMembers;
            public int craneGirders;
        }

        public class BridgeBuildResult
        {
            public int jointCount;
            public int deckMembers;
            public int supportMembers;
            public int cableMembers;
            public int shellElements;
        }

        public class PressureVesselBuildResult
        {
            public int jointCount;
            public int frameMembers;
            public int shellElements;
        }

        public class SpecialStructureBuildResult
        {
            public int jointCount;
            public int frameMembers;
            public int braceMembers;
            public int cableMembers;
            public int shellElements;
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

        public static IndustrialBuildResult BuildIndustrialStructure(cSapModel model, IndustrialStructureSpec spec)
        {
            if (model == null || spec == null)
                throw new ArgumentNullException();

            double span = spec.span > 0 ? spec.span : 24.0;
            double baySpacingDefault = spec.baySpacing > 0 ? spec.baySpacing : 6.0;
            double baseElevation = spec.baseElevation;
            double eaveHeight = spec.eaveHeight > 0 ? spec.eaveHeight : 8.0;
            double ridgeHeight = spec.ridgeHeight > 0 ? spec.ridgeHeight : eaveHeight + span * (spec.roof?.slopeRatio ?? 0.2) / 2.0;

            int bayCount = spec.bayCount > 0 ? spec.bayCount : 0;
            if (bayCount == 0 && spec.length > 0 && baySpacingDefault > 0)
                bayCount = Math.Max(1, (int)Math.Round(spec.length / baySpacingDefault));
            if (bayCount <= 0)
                bayCount = 5;

            double[] baySpacings = new double[bayCount];
            for (int i = 0; i < bayCount; i++)
                baySpacings[i] = baySpacingDefault;

            if (spec.bayOverrides != null)
            {
                foreach (var ov in spec.bayOverrides)
                {
                    if (ov == null) continue;
                    if (ov.bayIndex < 0 || ov.bayIndex >= baySpacings.Length) continue;
                    if (ov.baySpacing.HasValue && ov.baySpacing.Value > 0)
                        baySpacings[ov.bayIndex] = ov.baySpacing.Value;
                }
            }

            double totalLength = baySpacings.Sum();
            if (spec.length > 0)
                totalLength = spec.length;

            int framePlaneCount = bayCount + 1;
            int columnLines = Math.Max(1, spec.aisleCount);
            int columnLineCount = columnLines + 1;
            double halfSpan = span / 2.0;
            double columnSpacing = columnLines > 0 ? span / columnLines : span;

            var units = ResolveUnits(spec.units);
            int retUnits = model.SetPresentUnits(units);
            if (retUnits != 0)
                throw new ApplicationException("Failed to set units before industrial structure build.");

            var pointCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var fixedPoints = new HashSet<string>(StringComparer.Ordinal);
            var definedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string columnSection = EnsureFrameSection(model, definedSections, spec.frames?.columns, "Column450x350", 0.45, 0.35, "A992Fy50");
            string rafterSection = EnsureFrameSection(model, definedSections, spec.frames?.rafters, "Rafter400x300", 0.40, 0.30, "A992Fy50");
            string braceSection = EnsureFrameSection(model, definedSections, spec.frames?.braces, spec.bracing?.braceSection ?? "Brace200x200", 0.20, 0.20, "A572Gr50");
            string purlinSection = EnsureFrameSection(model, definedSections, spec.frames?.purlins, spec.roof?.purlinSection ?? "Purlin200x100", 0.20, 0.10, "A36");
            string craneSection = EnsureFrameSection(model, definedSections, spec.frames?.craneGirders, spec.crane?.runwaySection ?? "CraneGirder400", 0.50, 0.30, "A572Gr50");

            var basePoints = new string[framePlaneCount, columnLineCount];
            var eavePoints = new string[framePlaneCount, columnLineCount];
            var cranePoints = new string[framePlaneCount, 2];
            var ridgePoints = new string[framePlaneCount];

            double currentY = -totalLength / 2.0;

            var result = new IndustrialBuildResult();

            string AddPoint(double x, double y, double z)
            {
                return AddOrGetPoint(model, pointCache, x, y, z);
            }

            void AddFrame(string p1, string p2, string section, ref int counter)
            {
                if (string.IsNullOrWhiteSpace(p1) || string.IsNullOrWhiteSpace(p2) || string.Equals(p1, p2, StringComparison.Ordinal))
                    return;
                string name = string.Empty;
                int ret = model.FrameObj.AddByPoint(p1, p2, ref name, section);
                if (ret != 0)
                    throw new ApplicationException($"Failed to create frame between {p1} and {p2}.");
                counter++;
            }

            for (int plane = 0; plane < framePlaneCount; plane++)
            {
                double y = (plane == 0) ? currentY : currentY + baySpacings[plane - 1];
                if (plane > 0)
                    currentY = y;

                for (int line = 0; line < columnLineCount; line++)
                {
                    double x = -halfSpan + line * columnSpacing;
                    double baseZ = baseElevation;
                    double eaveZ = baseElevation + eaveHeight;

                    string basePoint = AddPoint(x, y, baseZ);
                    string topPoint = AddPoint(x, y, eaveZ);

                    basePoints[plane, line] = basePoint;
                    eavePoints[plane, line] = topPoint;

                    if (spec.fixBase && fixedPoints.Add(basePoint))
                    {
                        var restraints = new bool[] { true, true, true, true, true, true };
                        int ret = model.PointObj.SetRestraint(basePoint, ref restraints);
                        if (ret != 0)
                            throw new ApplicationException("Failed to fix base support in industrial structure.");
                    }

                    AddFrame(basePoint, topPoint, columnSection, ref result.frameMembers);
                }

                bool pitchedRoof = !string.Equals(spec.roof?.system, "Flat", StringComparison.OrdinalIgnoreCase);
                double ridgeOffset = spec.roof?.ridgeOffset ?? 0.0;
                double ridgeZ = baseElevation + ridgeHeight;
                string ridgePoint = null;

                if (pitchedRoof)
                {
                    ridgePoint = AddPoint(ridgeOffset, y, ridgeZ);
                    ridgePoints[plane] = ridgePoint;

                    for (int line = 0; line < columnLineCount; line++)
                    {
                        AddFrame(eavePoints[plane, line], ridgePoint, rafterSection, ref result.roofMembers);
                    }
                }
                else
                {
                    for (int line = 0; line < columnLineCount - 1; line++)
                    {
                        AddFrame(eavePoints[plane, line], eavePoints[plane, line + 1], rafterSection, ref result.roofMembers);
                    }
                }

                if (spec.crane?.enabled ?? false)
                {
                    double craneZ = baseElevation + Math.Max(0.5, spec.crane.runwayElevation);
                    string leftCrane = AddPoint(-halfSpan + spec.crane.inset, y, craneZ);
                    string rightCrane = AddPoint(halfSpan - spec.crane.inset, y, craneZ);
                    cranePoints[plane, 0] = leftCrane;
                    cranePoints[plane, 1] = rightCrane;
                    AddFrame(eavePoints[plane, 0], leftCrane, braceSection, ref result.braceMembers);
                    AddFrame(eavePoints[plane, columnLineCount - 1], rightCrane, braceSection, ref result.braceMembers);
                }
            }

            for (int plane = 0; plane < framePlaneCount - 1; plane++)
            {
                for (int line = 0; line < columnLineCount; line++)
                {
                    AddFrame(eavePoints[plane, line], eavePoints[plane + 1, line], purlinSection, ref result.roofMembers);
                    AddFrame(basePoints[plane, line], basePoints[plane + 1, line], columnSection, ref result.frameMembers);
                }

                if (spec.roof?.addPurlins ?? true)
                {
                    if (!string.Equals(spec.roof?.system, "Flat", StringComparison.OrdinalIgnoreCase))
                        AddFrame(ridgePoints[plane], ridgePoints[plane + 1], purlinSection, ref result.roofMembers);
                }

                if (spec.crane?.enabled ?? false)
                {
                    AddFrame(cranePoints[plane, 0], cranePoints[plane + 1, 0], craneSection, ref result.craneGirders);
                    AddFrame(cranePoints[plane, 1], cranePoints[plane + 1, 1], craneSection, ref result.craneGirders);
                }

                if (spec.bracing?.longitudinalBracing ?? true)
                {
                    AddFrame(eavePoints[plane, 0], eavePoints[plane + 1, columnLineCount - 1], braceSection, ref result.braceMembers);
                    AddFrame(eavePoints[plane, columnLineCount - 1], eavePoints[plane + 1, 0], braceSection, ref result.braceMembers);
                }
            }

            if ((spec.bracing?.portalBracing ?? true) && framePlaneCount > 1)
            {
                AddFrame(basePoints[0, 0], eavePoints[1, 0], braceSection, ref result.braceMembers);
                AddFrame(eavePoints[0, 0], basePoints[1, 0], braceSection, ref result.braceMembers);
                AddFrame(basePoints[0, columnLineCount - 1], eavePoints[1, columnLineCount - 1], braceSection, ref result.braceMembers);
                AddFrame(eavePoints[0, columnLineCount - 1], basePoints[1, columnLineCount - 1], braceSection, ref result.braceMembers);
            }

            if ((spec.bracing?.roofBracing ?? true) && framePlaneCount > 1 && !string.Equals(spec.roof?.system, "Flat", StringComparison.OrdinalIgnoreCase))
            {
                AddFrame(ridgePoints[0], eavePoints[1, 0], braceSection, ref result.braceMembers);
                AddFrame(ridgePoints[0], eavePoints[1, columnLineCount - 1], braceSection, ref result.braceMembers);
            }

            result.jointCount = pointCache.Count;
            model.Analyze.SetRunCaseFlag("DEAD", true);
            return result;
        }

        public static PressureVesselBuildResult BuildPressureVessel(cSapModel model, TankSpec spec)
        {
            if (model == null || spec == null)
                throw new ArgumentNullException();

            string type = spec.type ?? string.Empty;
            type = type.Trim();

            if (string.IsNullOrWhiteSpace(type) || type.Equals("Vertical", StringComparison.OrdinalIgnoreCase) || type.Equals("VerticalCylindrical", StringComparison.OrdinalIgnoreCase) || type.Equals("Cylindrical", StringComparison.OrdinalIgnoreCase))
            {
                var result = new PressureVesselBuildResult();
                int frames = BuildCylindricalReservoirFrames(model, spec);
                int nCirc = Math.Max(12, spec.geometry?.numWallSegments ?? 24);
                int nZ = Math.Max(2, spec.geometry?.numHeightSegments ?? 8);
                result.frameMembers = frames;
                result.jointCount = nCirc * nZ;
                return result;
            }

            var units = ResolveUnits(spec.units);
            if (model.SetPresentUnits(units) != 0)
                throw new ApplicationException("Failed to set units for pressure vessel build.");

            var pointCache = new Dictionary<string, string>(StringComparer.Ordinal);

            string AddPoint(double x, double y, double z)
            {
                return AddOrGetPoint(model, pointCache, x, y, z);
            }

            void AddFrame(string p1, string p2, ref int counter)
            {
                if (string.IsNullOrWhiteSpace(p1) || string.IsNullOrWhiteSpace(p2) || string.Equals(p1, p2, StringComparison.Ordinal))
                    return;
                string name = string.Empty;
                int ret = model.FrameObj.AddByPoint(p1, p2, ref name);
                if (ret != 0)
                    throw new ApplicationException("Failed to create pressure vessel frame member.");
                counter++;
            }

            var result = new PressureVesselBuildResult();

            if (type.Equals("Horizontal", StringComparison.OrdinalIgnoreCase) || type.Equals("HorizontalCylindrical", StringComparison.OrdinalIgnoreCase))
            {
                double diameter = spec.geometry?.diameter > 0 ? spec.geometry.diameter : 8.0;
                double length = spec.geometry?.length > 0 ? spec.geometry.length : Math.Max(8.0, spec.geometry?.height ?? 12.0);
                int nCirc = Math.Max(16, spec.geometry?.numWallSegments ?? 24);
                int nAxial = Math.Max(4, spec.geometry?.numHeightSegments ?? 12);
                double radius = diameter / 2.0;
                double baseElevation = spec.foundationElevation;
                double centerZ = baseElevation + radius;
                double startX = -length / 2.0;
                double dx = length / nAxial;

                var ringPoints = new string[nAxial + 1, nCirc];
                for (int i = 0; i <= nAxial; i++)
                {
                    double x = startX + i * dx;
                    for (int j = 0; j < nCirc; j++)
                    {
                        double theta = 2 * Math.PI * j / nCirc;
                        double y = radius * Math.Cos(theta);
                        double z = centerZ + radius * Math.Sin(theta);
                        ringPoints[i, j] = AddPoint(x, y, z);
                    }
                }

                for (int i = 0; i <= nAxial; i++)
                {
                    for (int j = 0; j < nCirc; j++)
                    {
                        AddFrame(ringPoints[i, j], ringPoints[i, (j + 1) % nCirc], ref result.frameMembers);
                    }
                }

                for (int i = 0; i < nAxial; i++)
                {
                    for (int j = 0; j < nCirc; j++)
                    {
                        AddFrame(ringPoints[i, j], ringPoints[i + 1, j], ref result.frameMembers);
                    }
                }

                if (spec.fixBase)
                {
                    int bottomIndex = (3 * nCirc) / 4;
                    for (int i = 0; i <= nAxial; i++)
                    {
                        var bottomPoint = ringPoints[i, bottomIndex % nCirc];
                        var restraints = new bool[] { true, true, true, true, true, true };
                        model.PointObj.SetRestraint(bottomPoint, ref restraints);
                    }
                }

                result.jointCount = pointCache.Count;
                model.Analyze.SetRunCaseFlag("DEAD", true);
                return result;
            }

            if (type.Equals("Spherical", StringComparison.OrdinalIgnoreCase) || type.Equals("Sphere", StringComparison.OrdinalIgnoreCase))
            {
                double diameter = spec.geometry?.diameter > 0 ? spec.geometry.diameter : 10.0;
                double radius = spec.geometry?.radius > 0 ? spec.geometry.radius : diameter / 2.0;
                int nLat = Math.Max(6, spec.geometry?.numHeightSegments ?? 12);
                int nLon = Math.Max(12, spec.geometry?.numWallSegments ?? 24);
                double baseElevation = spec.foundationElevation;
                double centerZ = baseElevation + radius;

                var spherePoints = new string[nLat + 1, nLon];

                for (int lat = 0; lat <= nLat; lat++)
                {
                    double phi = Math.PI * lat / nLat;
                    double sinPhi = Math.Sin(phi);
                    double cosPhi = Math.Cos(phi);
                    for (int lon = 0; lon < nLon; lon++)
                    {
                        double theta = 2 * Math.PI * lon / nLon;
                        double x = radius * sinPhi * Math.Cos(theta);
                        double y = radius * sinPhi * Math.Sin(theta);
                        double z = centerZ + radius * cosPhi;
                        spherePoints[lat, lon] = AddPoint(x, y, z);
                    }
                }

                for (int lat = 0; lat <= nLat; lat++)
                {
                    for (int lon = 0; lon < nLon; lon++)
                    {
                        AddFrame(spherePoints[lat, lon], spherePoints[lat, (lon + 1) % nLon], ref result.frameMembers);
                    }
                }

                for (int lat = 0; lat < nLat; lat++)
                {
                    for (int lon = 0; lon < nLon; lon++)
                    {
                        AddFrame(spherePoints[lat, lon], spherePoints[lat + 1, lon], ref result.frameMembers);
                    }
                }

                if (spec.fixBase)
                {
                    int baseLat = nLat;
                    for (int lon = 0; lon < nLon; lon++)
                    {
                        var point = spherePoints[baseLat, lon];
                        var restraints = new bool[] { true, true, true, true, true, true };
                        model.PointObj.SetRestraint(point, ref restraints);
                    }
                }

                result.jointCount = pointCache.Count;
                model.Analyze.SetRunCaseFlag("DEAD", true);
                return result;
            }

            // Default fallback: call cylindrical builder
            var fallback = new PressureVesselBuildResult();
            int fallbackFrames = BuildCylindricalReservoirFrames(model, spec);
            fallback.frameMembers = fallbackFrames;
            int fallbackNCirc = Math.Max(12, spec.geometry?.numWallSegments ?? 24);
            int fallbackNZ = Math.Max(2, spec.geometry?.numHeightSegments ?? 8);
            fallback.jointCount = fallbackNCirc * fallbackNZ;
            return fallback;
        }

        public static BridgeBuildResult BuildBridgeStructure(cSapModel model, BridgeDesignSpec spec)
        {
            if (model == null || spec == null)
                throw new ArgumentNullException();

            var spans = (spec.spans != null && spec.spans.Count > 0)
                ? spec.spans.Where(s => s != null && s.length > 0).ToList()
                : new List<BridgeSpanSpec> { new BridgeSpanSpec { length = 35.0 }, new BridgeSpanSpec { length = 45.0 }, new BridgeSpanSpec { length = 35.0 } };

            if (spans.Count == 0)
                spans.Add(new BridgeSpanSpec { length = 40.0 });

            int segmentsPerSpan = Math.Max(2, spec.segmentsPerSpan);
            double deckWidth = spec.deckWidth > 0 ? spec.deckWidth : 12.0;
            int girderLines = Math.Max(2, spec.girders);
            double deckElevation = spec.deckElevation;
            double foundationElevation = spec.supports?.foundationElevation ?? 0.0;
            double pierHeight = spec.supports?.pierHeight ?? (deckElevation - foundationElevation);
            bool fixPierBases = spec.supports?.fixBase ?? true;

            var units = ResolveUnits(spec.units);
            if (model.SetPresentUnits(units) != 0)
                throw new ApplicationException("Failed to set units for bridge build.");

            var pointCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var fixedPoints = new HashSet<string>(StringComparer.Ordinal);
            var definedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string girderSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.superstructure?.girderSection ?? "Girder900x300",
                material = "A709Gr50",
                depth = 0.9,
                width = 0.3
            }, spec.superstructure?.girderSection ?? "Girder900x300", 0.9, 0.3, "A709Gr50");

            string crossSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.superstructure?.crossBeamSection ?? "CrossBeam600x250",
                material = "A709Gr50",
                depth = 0.6,
                width = 0.25
            }, spec.superstructure?.crossBeamSection ?? "CrossBeam600x250", 0.6, 0.25, "A709Gr50");

            string cableSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.superstructure?.cableSection ?? "CableRod120",
                material = "SteelCable",
                depth = 0.12,
                width = 0.12
            }, spec.superstructure?.cableSection ?? "CableRod120", 0.12, 0.12, "SteelCable");

            string archSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.superstructure?.archSection ?? "ArchBox800x400",
                material = "A709Gr50",
                depth = 0.8,
                width = 0.4
            }, spec.superstructure?.archSection ?? "ArchBox800x400", 0.8, 0.4, "A709Gr50");

            string trussSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.superstructure?.trussSection ?? "TrussMember400x250",
                material = "A709Gr50",
                depth = 0.4,
                width = 0.25
            }, spec.superstructure?.trussSection ?? "TrussMember400x250", 0.4, 0.25, "A709Gr50");

            string AddPoint(double x, double y, double z)
            {
                return AddOrGetPoint(model, pointCache, x, y, z);
            }

            void AddFrame(string p1, string p2, string section, ref int counter)
            {
                if (string.IsNullOrWhiteSpace(p1) || string.IsNullOrWhiteSpace(p2) || string.Equals(p1, p2, StringComparison.Ordinal))
                    return;
                string name = string.Empty;
                int ret = model.FrameObj.AddByPoint(p1, p2, ref name, section);
                if (ret != 0)
                    throw new ApplicationException($"Failed to create bridge member between {p1} and {p2}.");
                counter++;
            }

            double totalLength = spans.Sum(s => s.length);
            double startX = -totalLength / 2.0;
            var stationPositions = new List<double> { startX };
            double running = startX;

            for (int i = 0; i < spans.Count; i++)
            {
                double spanLength = spans[i].length;
                double dx = spanLength / segmentsPerSpan;
                for (int seg = 1; seg <= segmentsPerSpan; seg++)
                {
                    running += dx;
                    stationPositions.Add(running);
                }
            }

            int girderCount = girderLines;
            double girderSpacing = girderCount > 1 ? deckWidth / (girderCount - 1) : deckWidth;
            var deckPoints = new string[stationPositions.Count, girderCount];

            for (int s = 0; s < stationPositions.Count; s++)
            {
                double x = stationPositions[s];
                for (int g = 0; g < girderCount; g++)
                {
                    double y = -deckWidth / 2.0 + g * girderSpacing;
                    deckPoints[s, g] = AddPoint(x, y, deckElevation);
                }
            }

            var result = new BridgeBuildResult();

            for (int g = 0; g < girderCount; g++)
            {
                for (int s = 0; s < stationPositions.Count - 1; s++)
                {
                    AddFrame(deckPoints[s, g], deckPoints[s + 1, g], girderSection, ref result.deckMembers);
                }
            }

            for (int s = 0; s < stationPositions.Count; s++)
            {
                for (int g = 0; g < girderCount - 1; g++)
                {
                    AddFrame(deckPoints[s, g], deckPoints[s, g + 1], crossSection, ref result.deckMembers);
                }
            }

            int stationIndex = 0;
            int spanIndex = 0;
            for (int s = 0; s < stationPositions.Count; s++)
            {
                bool isSupport = (s == 0) || (s == stationPositions.Count - 1);
                if (!isSupport && spanIndex < spans.Count)
                {
                    double distanceFromStart = stationPositions[s] - startX;
                    double cumulative = 0;
                    for (int k = 0; k < spans.Count; k++)
                    {
                        cumulative += spans[k].length;
                        if (Math.Abs(distanceFromStart - cumulative) < 1e-6)
                        {
                            isSupport = true;
                            spanIndex = k + 1;
                            break;
                        }
                    }
                }

                if (!isSupport)
                    continue;

                double pierTopZ = deckElevation;
                double pierBaseZ = foundationElevation;
                double x = stationPositions[s];

                for (int col = 0; col < (spec.supports?.columnsPerPier ?? 2); col++)
                {
                    double offset = (girderSpacing * (girderCount - 1)) * 0.25;
                    double y = -offset + col * (2 * offset / Math.Max(1, (spec.supports?.columnsPerPier ?? 2) - 1));
                    string basePoint = AddPoint(x, y, pierBaseZ);
                    string topPoint = AddPoint(x, y, pierTopZ);
                    AddFrame(basePoint, topPoint, girderSection, ref result.supportMembers);

                    if (fixPierBases && fixedPoints.Add(basePoint))
                    {
                        var restraints = new bool[] { true, true, true, true, true, true };
                        if (model.PointObj.SetRestraint(basePoint, ref restraints) != 0)
                            throw new ApplicationException("Failed to restrain pier base.");
                    }
                }
            }

            string type = spec.bridgeType ?? "Girder";

            if (type.Equals("CableStayed", StringComparison.OrdinalIgnoreCase))
            {
                double towerHeight = spec.superstructure?.towerHeight ?? (deckElevation + 35.0);
                for (int s = 0; s < stationPositions.Count; s++)
                {
                    bool isTower = (s == 0) || (s == stationPositions.Count - 1);
                    if (!isTower)
                    {
                        double distance = stationPositions[s] - startX;
                        double cumulative = 0;
                        for (int k = 0; k < spans.Count; k++)
                        {
                            cumulative += spans[k].length;
                            if (Math.Abs(distance - cumulative) < 1e-6 && k < spans.Count - 1)
                            {
                                isTower = true;
                                break;
                            }
                        }
                    }

                    if (!isTower) continue;

                    string towerTop = AddPoint(stationPositions[s], 0, towerHeight);
                    string towerBase = AddPoint(stationPositions[s], 0, foundationElevation);
                    AddFrame(towerBase, towerTop, girderSection, ref result.supportMembers);

                    int range = Math.Min(segmentsPerSpan, stationPositions.Count - s - 1);
                    for (int g = 0; g < girderCount; g++)
                    {
                        for (int k = 1; k <= range; k++)
                        {
                            AddFrame(towerTop, deckPoints[Math.Min(stationPositions.Count - 1, s + k), g], cableSection, ref result.cableMembers);
                            if (s - k >= 0)
                                AddFrame(towerTop, deckPoints[Math.Max(0, s - k), g], cableSection, ref result.cableMembers);
                        }
                    }
                }
            }
            else if (type.Equals("Arch", StringComparison.OrdinalIgnoreCase))
            {
                double currentStart = startX;
                int stationPointer = 0;
                foreach (var span in spans)
                {
                    double archRise = span.length * (spec.superstructure?.archRiseRatio ?? 0.2);
                    int divs = segmentsPerSpan * 2;
                    string[] archNodes = new string[divs + 1];
                    for (int i = 0; i <= divs; i++)
                    {
                        double t = (double)i / divs;
                        double x = currentStart + t * span.length;
                        double z = deckElevation + Math.Sin(Math.PI * t) * archRise;
                        archNodes[i] = AddPoint(x, 0, z);
                    }

                    for (int i = 0; i < divs; i++)
                    {
                        AddFrame(archNodes[i], archNodes[i + 1], archSection, ref result.deckMembers);
                    }

                    for (int i = 0; i <= divs; i++)
                    {
                        int deckIndex = Math.Min(deckPoints.GetLength(0) - 1, stationPointer + Math.Min(i / 2, segmentsPerSpan));
                        for (int g = 0; g < girderCount; g++)
                        {
                            AddFrame(archNodes[i], deckPoints[deckIndex, g], cableSection, ref result.cableMembers);
                        }
                    }

                    currentStart += span.length;
                    stationPointer += segmentsPerSpan;
                }
            }
            else if (type.Equals("Truss", StringComparison.OrdinalIgnoreCase))
            {
                double trussHeight = spec.superstructure?.trussHeight ?? 6.0;
                for (int s = 0; s < stationPositions.Count; s++)
                {
                    for (int g = 0; g < girderCount; g++)
                    {
                        string topNode = AddPoint(stationPositions[s], -deckWidth / 2.0 + g * girderSpacing, deckElevation + trussHeight);
                        AddFrame(deckPoints[s, g], topNode, trussSection, ref result.cableMembers);
                        if (s < stationPositions.Count - 1)
                            AddFrame(topNode, AddPoint(stationPositions[s + 1], -deckWidth / 2.0 + g * girderSpacing, deckElevation + trussHeight), trussSection, ref result.deckMembers);
                        if (g < girderCount - 1)
                            AddFrame(topNode, AddPoint(stationPositions[s], -deckWidth / 2.0 + (g + 1) * girderSpacing, deckElevation + trussHeight), trussSection, ref result.deckMembers);
                    }
                }
            }

            if (spec.superstructure?.addDiaphragms ?? true)
            {
                for (int s = 0; s < stationPositions.Count - 1; s += Math.Max(1, segmentsPerSpan / 2))
                {
                    AddFrame(deckPoints[s, 0], deckPoints[s + 1, girderCount - 1], crossSection, ref result.deckMembers);
                    AddFrame(deckPoints[s, girderCount - 1], deckPoints[s + 1, 0], crossSection, ref result.deckMembers);
                }
            }

            result.jointCount = pointCache.Count;
            model.Analyze.SetRunCaseFlag("DEAD", true);
            return result;
        }

        public static SpecialStructureBuildResult BuildSpecialStructure(cSapModel model, SpecialStructureSpec spec)
        {
            if (model == null || spec == null)
                throw new ArgumentNullException();

            var units = ResolveUnits(spec.units);
            if (model.SetPresentUnits(units) != 0)
                throw new ApplicationException("Failed to set units for special structure build.");

            string AddPoint(double x, double y, double z)
            {
                return AddOrGetPoint(model, pointCache, x, y, z);
            }

            void AddFrame(string p1, string p2, string section, ref int counter)
            {
                if (string.IsNullOrWhiteSpace(p1) || string.IsNullOrWhiteSpace(p2) || string.Equals(p1, p2, StringComparison.Ordinal))
                    return;
                string name = string.Empty;
                int ret = model.FrameObj.AddByPoint(p1, p2, ref name, section);
                if (ret != 0)
                    throw new ApplicationException($"Failed to create special structure member between {p1} and {p2}.");
                counter++;
            }

            int AddShell(string[] points, string property)
            {
                string areaName = string.Empty;
                string prop = property;
                int ret = model.AreaObj.AddByPoint(points, ref areaName, ref prop);
                if (ret != 0)
                    throw new ApplicationException("Failed to create shell area for special structure.");
                return 1;
            }

            var pointCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var definedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new SpecialStructureBuildResult();

            string braceSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.tower?.braceSection ?? "TowerBrace200x150",
                material = "A572Gr50",
                depth = 0.2,
                width = 0.15
            }, spec.tower?.braceSection ?? "TowerBrace200x150", 0.2, 0.15, "A572Gr50");

            string legSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.tower?.legSection ?? "TowerLeg350x250",
                material = "A572Gr50",
                depth = 0.35,
                width = 0.25
            }, spec.tower?.legSection ?? "TowerLeg350x250", 0.35, 0.25, "A572Gr50");

            string cableSection = EnsureFrameSection(model, definedSections, new IndustrialFrameSection
            {
                name = spec.membrane?.edgeCableSection ?? "EdgeCable90",
                material = "CableSteel",
                depth = 0.09,
                width = 0.09
            }, spec.membrane?.edgeCableSection ?? "EdgeCable90", 0.09, 0.09, "CableSteel");

            string membraneProperty = spec.membrane?.membraneSection ?? "MembranePTFE";
            EnsureShellSection(model, membraneProperty, new ShellSectionDefinition
            {
                name = membraneProperty,
                material = spec.membrane?.membraneSection ?? "MembranePTFE",
                thickness = 0.015
            }, spec.membrane?.membraneSection ?? "MembranePTFE");

            double radius = Math.Max(1.0, spec.radius);
            double height = Math.Max(1.0, spec.height);
            double baseZ = spec.baseElevation;
            int rings = Math.Max(1, spec.rings);
            int segments = Math.Max(6, spec.segments);

            if (spec.structureType.Equals("SpaceFrame", StringComparison.OrdinalIgnoreCase) || spec.structureType.Equals("Dome", StringComparison.OrdinalIgnoreCase))
            {
                string apex = AddPoint(0, 0, baseZ + height);
                string[] previousRing = null;
                for (int ring = 1; ring <= rings; ring++)
                {
                    double ratio = (double)ring / (rings + 1);
                    double ringRadius = radius * ratio;
                    double ringZ = baseZ + height * (1 - ratio * 0.9);
                    string[] thisRing = new string[segments];
                    for (int seg = 0; seg < segments; seg++)
                    {
                        double theta = 2 * Math.PI * seg / segments;
                        double x = ringRadius * Math.Cos(theta);
                        double y = ringRadius * Math.Sin(theta);
                        thisRing[seg] = AddPoint(x, y, ringZ);
                        AddFrame(thisRing[seg], apex, braceSection, ref result.frameMembers);
                        if (previousRing != null)
                        {
                            AddFrame(thisRing[seg], previousRing[seg], braceSection, ref result.frameMembers);
                            AddFrame(thisRing[seg], previousRing[(seg + 1) % segments], braceSection, ref result.frameMembers);
                        }
                        AddFrame(thisRing[seg], thisRing[(seg + 1) % segments], braceSection, ref result.frameMembers);
                    }
                    previousRing = thisRing;
                }
            }
            else if (spec.structureType.Equals("Membrane", StringComparison.OrdinalIgnoreCase) || spec.structureType.Equals("Tension", StringComparison.OrdinalIgnoreCase))
            {
                string[] edge = new string[segments];
                for (int seg = 0; seg < segments; seg++)
                {
                    double theta = 2 * Math.PI * seg / segments;
                    edge[seg] = AddPoint(radius * Math.Cos(theta), radius * Math.Sin(theta), baseZ);
                }
                for (int seg = 0; seg < segments; seg++)
                {
                    AddFrame(edge[seg], edge[(seg + 1) % segments], cableSection, ref result.cableMembers);
                }

                string center = AddPoint(0, 0, baseZ + height);
                for (int seg = 0; seg < segments; seg++)
                {
                    string[] panel = new[] { center, edge[seg], edge[(seg + 1) % segments] };
                    result.shellElements += AddShell(panel, membraneProperty);
                    AddFrame(center, edge[seg], cableSection, ref result.cableMembers);
                }
            }
            else if (spec.structureType.Equals("Tower", StringComparison.OrdinalIgnoreCase) || spec.structureType.Equals("TelecomTower", StringComparison.OrdinalIgnoreCase))
            {
                int sides = Math.Max(3, spec.tower?.sides ?? 4);
                int segs = Math.Max(1, spec.tower?.segments ?? 8);
                string[] previousLevel = null;
                for (int level = 0; level <= segs; level++)
                {
                    double ratio = (double)level / segs;
                    double levelRadius = radius * (1 - ratio * (spec.tower?.taperRatio ?? 0.15));
                    double z = baseZ + ratio * height;
                    string[] thisLevel = new string[sides];
                    for (int s = 0; s < sides; s++)
                    {
                        double theta = 2 * Math.PI * s / sides;
                        thisLevel[s] = AddPoint(levelRadius * Math.Cos(theta), levelRadius * Math.Sin(theta), z);
                        if (previousLevel != null)
                        {
                            AddFrame(thisLevel[s], previousLevel[s], legSection, ref result.frameMembers);
                            AddFrame(thisLevel[s], previousLevel[(s + 1) % sides], braceSection, ref result.braceMembers);
                        }
                        AddFrame(thisLevel[s], thisLevel[(s + 1) % sides], braceSection, ref result.braceMembers);
                    }
                    previousLevel = thisLevel;
                }
            }
            else if (spec.structureType.Equals("CoolingTower", StringComparison.OrdinalIgnoreCase))
            {
                int verticalDivs = Math.Max(3, rings);
                int circumDivs = Math.Max(12, segments);
                string[,] grid = new string[verticalDivs + 1, circumDivs];
                for (int v = 0; v <= verticalDivs; v++)
                {
                    double t = (double)v / verticalDivs;
                    double z = baseZ + height * t;
                    double r = radius * (0.6 + 0.4 * Math.Sin(Math.PI * t));
                    for (int c = 0; c < circumDivs; c++)
                    {
                        double theta = 2 * Math.PI * c / circumDivs;
                        grid[v, c] = AddPoint(r * Math.Cos(theta), r * Math.Sin(theta), z);
                    }
                }

                for (int v = 0; v < verticalDivs; v++)
                {
                    for (int c = 0; c < circumDivs; c++)
                    {
                        string p1 = grid[v, c];
                        string p2 = grid[v, (c + 1) % circumDivs];
                        string p3 = grid[v + 1, (c + 1) % circumDivs];
                        string p4 = grid[v + 1, c];
                        result.shellElements += AddShell(new[] { p1, p2, p3, p4 }, membraneProperty);
                    }
                }
            }

            if (spec.fixBase)
            {
                foreach (var kv in pointCache)
                {
                    var parts = kv.Key.Split('|');
                    if (parts.Length == 3)
                    {
                        if (double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var zCoord))
                        {
                            if (Math.Abs(zCoord - baseZ) <= 1e-6)
                            {
                                var restraints = new bool[] { true, true, true, true, true, true };
                                model.PointObj.SetRestraint(kv.Value, ref restraints);
                            }
                        }
                    }
                }
            }

            result.jointCount = pointCache.Count;
            model.Analyze.SetRunCaseFlag("DEAD", true);
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

            object Optional(object value) => value ?? Type.Missing;

            var argumentSets = new List<object[]>
            {
                new object[] { propertyName, shellValue, material, thickness, membrane, bending, shear, thermal },
                new object[] { propertyName, shellValue, material, thickness },
                new object[] { propertyName, shellValue, material, thickness, 0, string.Empty, string.Empty },
                new object[] { propertyName, shellValue, material, thickness, membrane, bending, shear, thermal, 0.0, 0.0, 0.0, 0.0 },
                new object[] { propertyName, shellValue, material, thickness, 0, string.Empty, string.Empty, 0.0, 0.0, 0.0, 0.0 },
                new object[] { propertyName, shellValue, material, thickness, Optional(membrane), Optional(bending), Optional(shear), Optional(thermal), Type.Missing, Type.Missing, Type.Missing, Type.Missing },
                new object[] { propertyName, shellValue, material, thickness, Optional(membrane), Optional(bending), Optional(shear), Optional(thermal), Optional(0.0), Optional(0.0), Optional(0.0), Optional(0.0) }
            };

            return InvokeComMethod(propArea, "SetShell", argumentSets);
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

        private static int InvokeComMethod(object comObject, string methodName, IEnumerable<object[]> argumentSets)
        {
            if (comObject == null) throw new ArgumentNullException(nameof(comObject));

            const BindingFlags flags = BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.OptionalParamBinding;
            Type comType = comObject.GetType();
            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var args in argumentSets ?? Enumerable.Empty<object[]>())
            {
                if (args == null)
                {
                    continue;
                }
                foreach (var candidate in EnumerateCandidateMethodNames(comType, methodName))
                {
                    if (!tried.Add(candidate + "|" + args.Length.ToString(CultureInfo.InvariantCulture)))
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
                    catch (TargetInvocationException tie) when (tie.InnerException is COMException comEx && IsDispatchRetryWorthy(comEx))
                    {
                        continue;
                    }
                    catch (TargetParameterCountException)
                    {
                        continue;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }
            }

            throw new MissingMethodException($"Method '{methodName}' not found on type '{comType?.FullName}'.");
        }

        private static bool IsDispatchRetryWorthy(COMException comException)
        {
            if (comException == null)
                return false;

            const int DISP_E_MEMBERNOTFOUND = unchecked((int)0x80020003);
            const int DISP_E_BADPARAMCOUNT = unchecked((int)0x8002000E);
            const int DISP_E_TYPEMISMATCH = unchecked((int)0x80020005);

            return comException.ErrorCode == DISP_E_BADPARAMCOUNT
                || comException.ErrorCode == DISP_E_TYPEMISMATCH
                || comException.ErrorCode == DISP_E_MEMBERNOTFOUND;
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

        private static string EnsureFrameSection(cSapModel model, HashSet<string> defined, IndustrialFrameSection section, string fallbackName, double fallbackDepth, double fallbackWidth, string fallbackMaterial)
        {
            string name = section?.name;
            if (string.IsNullOrWhiteSpace(name))
                name = fallbackName;

            string material = !string.IsNullOrWhiteSpace(section?.material) ? section.material : fallbackMaterial;
            double depth = section?.depth > 0 ? section.depth : fallbackDepth;
            double width = section?.width > 0 ? section.width : fallbackWidth;

            if (string.IsNullOrWhiteSpace(material))
                material = fallbackMaterial;

            if (string.IsNullOrWhiteSpace(name))
                name = "FrameSection";

            if (defined == null)
                defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!defined.Contains(name))
            {
                int ret = model.PropFrame.SetRectangle(name, material, depth, width);
                if (ret != 0)
                    throw new ApplicationException($"Failed to define frame section '{name}'.");
                defined.Add(name);
            }

            return name;
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
