using System;
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

            // Create vertical frames
            for (int ic = 0; ic < nCirc; ic++)
            {
                for (int iz = 0; iz < nZ - 1; iz++)
                {
                    string name = "";
                    ret = model.FrameObj.AddByPoint(pNames[iz, ic], pNames[iz + 1, ic], ref name);
                    if (ret != 0) throw new ApplicationException("Frame add failed (vertical).");
                    created++;
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

            // TODO: you can assign section properties, materials, loads here based on spec.
            // Example (pseudo):
            // model.PropFrame.SetPipe("WALL", matName, outerDiam, thickness);
            // model.FrameObj.SetSection(name, "WALL");

            return created;
        }
    }
}
