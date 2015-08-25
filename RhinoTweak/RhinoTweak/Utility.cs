using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    static class Utility
    {

        internal static double myAngleFinder(Vector3d v1tov2, Vector3d v1tov3)
        {
            double dotProduct = v1tov2 * v1tov3;
            double productOfLengths = v1tov3.Length * v1tov2.Length;
            double angleRadians = Math.Acos(dotProduct / productOfLengths);
            double angle = angleRadians * (180 / Math.PI);
            return angle;
        }

        internal static Point3d getPoint3dforIndex(int index, Mesh housingMesh)
        {
            return housingMesh.Vertices[index]; 
        }
    }
}
