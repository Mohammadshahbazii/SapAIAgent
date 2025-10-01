using System;
using System.Collections.Generic;
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

                    if (jointLoads != null && !jointLoads.ContainsKey(pName))
                        jointLoads[pName] = (0.0, 0.0);
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

                        if (jointLoads.ContainsKey(topPoint))
                        {
                            var load = jointLoads[topPoint];
                            jointLoads[topPoint] = (load.Fx + halfForceX, load.Fy + halfForceY);
                        }
                        if (jointLoads.ContainsKey(bottomPoint))
                        {
                            var load = jointLoads[bottomPoint];
                            jointLoads[bottomPoint] = (load.Fx + halfForceX, load.Fy + halfForceY);
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
                    var forces = new double[] { kvp.Value.Fx, kvp.Value.Fy, 0, 0, 0, 0 };
                    int retLoad = model.PointObj.SetLoadForce(kvp.Key, hydroPattern, ref forces, false);
                    if (retLoad != 0) throw new ApplicationException($"Failed to assign hydrostatic load at joint {kvp.Key}.");
                }
            }

            return created;
        }
    }
}
