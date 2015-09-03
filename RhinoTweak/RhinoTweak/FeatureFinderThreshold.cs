using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class FeatureFinderThreshold : FeatureFinderAbstractBase
    {

        public FeatureFinderThreshold(Mesh housingMesh, RhinoDoc doc) 
            : base(housingMesh, doc)
        {
            // not much to do . 
        }

        /// <summary>
        /// tell me how you want the vertices colored.  
        /// </summary>
        /// <returns></returns>
        internal override Color[] colorize()
        {
            return vertexColors;
        }

        internal override void findFeatures()
        {
            // go through all the vertices. 
            for (int i = 0; i < housingMesh.Vertices.Count; i+= Constants.curvatureVertexIncrement)
            {
                Boolean isAFeature = true; 
                // find all the level 2 neighbors.
                List<int> neighborIndices2ndGenOnly = new List<int>();
                neighborIndices2ndGenOnly.AddRange(getOnlyNthGeneration(1, i));
                vertexColors[i] = Color.Bisque; 
                // calculate dz for each neighbor.  
                foreach (int neighborIndex in neighborIndices2ndGenOnly)
                {
                    // color the neighbors green. 
                    //pendingColorChanges.Add(new ColorChange(neighborIndex, Color.Green));
                    // draw the neighbor normal 
                    double dx = 0;
                    double dz = 0;
                    calculateDxAndDz(i, neighborIndex, ref dx, ref dz);
                    double curvature = -dz / dx;
                    //RhinoLog.debug(i + " neighbor curvature " + curvature); 
                    // if curvature for any neighbor is too big or too small, 
                    if (curvature > Constants.maxThresholdForCurvatureInFeatureIdentification || 
                        curvature < Constants.minThresholdForCurvatureInFeatureIdentification)
                    {
                        // then this is not a feature.  
                      //  RhinoLog.debug("--> not a feature"); 
                        isAFeature = false;
                    }
                }
                if (isAFeature)
                {
                    surfaceFeatures.Add(new SurfaceFeature(i, SurfaceFeature.featureType.outie));
                    vertexColors[i] = Color.Red; 
                }
            }
        }
    }
}
