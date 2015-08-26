using System;
using Rhino;
using Rhino.Geometry;
using System.Drawing;
using System.Collections.Generic;

namespace RhinoTweak
{
    internal class CurvatureBasedFeatureFinder : AbstractFeatureFinder
    {
        private bool curvatureIsCalcuated;
        /// <summary>
        ///  indexed by vertex indices.  
        /// </summary>
        private double[] signedMeanCurvatures;
        private bool featuresAreFound;

        public CurvatureBasedFeatureFinder(Mesh housingMesh, RhinoDoc doc) 
            : base(housingMesh, doc)
        {
            curvatureIsCalcuated = false;
            signedMeanCurvatures = new double[housingMesh.Vertices.Count]; 
        }
        
        /// <summary>
        /// ignores innie feature points right now .
        /// uses a max and min positive curvature.  
        /// </summary>
        internal override void findFeatures()
        {
            // make sure the curvature is calculated. 
            if (!curvatureIsCalcuated)
            {
                //RhinoLog.write("calculating curvature...");
                calculateSignedMeanCurvature();
                //RhinoLog.write("done"); 
            }
            // find the features.  First look at curvature. 
            // large magnitude curvature means you are a feature. 
            for (int i = 0; i < housingMesh.Vertices.Count; i++)
            {
                if (signedMeanCurvatures[i] > Constants.curvatureFeatureThresholdLow &&
                    signedMeanCurvatures[i] < Constants.curvatureFeatureThresholdHigh)
                {
                    // we have an outie (point poking out with positive curvature) surface feature. 
                    surfaceFeatures.Add(new SurfaceFeature(i, SurfaceFeature.featureType.outie));
                }
            }
            // that's all the features so far.  
            // now rule out features by looking for flat spots.  
            HashSet<SurfaceFeature> flatSpots = findFlatSpots(surfaceFeatures);
            surfaceFeatures.ExceptWith(flatSpots);
            RhinoLog.write("found " + surfaceFeatures.Count + " surface features");
            //mergeDuplicates();
            RhinoLog.write("found " + surfaceFeatures.Count + " UNIQUE surface features");

            featuresAreFound = true;
            RhinoLog.write("removed " + flatSpots.Count + " features that weren't on cones"); 
        }

        private HashSet<SurfaceFeature> findFlatSpots(HashSet<SurfaceFeature> surfaceFeatures)
        {
            HashSet<SurfaceFeature> flatSpots = new HashSet<SurfaceFeature>();
            // go through the surface features so far. 
            // for each one find the 3rd gen neighbors. 
            // if more than say 5 of those are flat, that is, delta z < threshold. 
            // then this must be a flat spot.  
            foreach (SurfaceFeature sf in surfaceFeatures)
            {
                HashSet<int> verticesIn3rdGen = getOnlyNthGeneration(3, sf.indexIntoTheMeshVertexList);
                vertexColors[sf.indexIntoTheMeshVertexList] = Color.Red;
                int flatCount = 0;
                foreach (int neighborIndex in verticesIn3rdGen)
                {
                    double dx = 0;
                    double dz = 0;
                    calculateDxAndDz(sf.indexIntoTheMeshVertexList, neighborIndex, ref dx, ref dz);
                    if (Math.Abs(dz) < Constants.flatThresholdForDz)
                    {
                        flatCount++;
                        vertexColors[neighborIndex] = Color.Gold;
                    }
                }
                if (flatCount > Constants.minFlatsToBeConsideredNotAFeature)
                {
                    flatSpots.Add(sf);
                }
            }
            // let's color the flat spots. 
            foreach (SurfaceFeature sf in flatSpots)
            {
                int vertexIndex = sf.indexIntoTheMeshVertexList;
                vertexColors[vertexIndex] = Color.DarkGreen;
            }
            return flatSpots;
        }




        private void calculateSignedMeanCurvature()
        {
            for (int i = 0; i < housingMesh.Vertices.Count; i += Constants.curvatureVertexIncrement)
            {
                Point3d theVertex = housingMesh.Vertices[i];
                // draw the vertex normal. 
                //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 10.0));

                // get the neighbors. 
                // three ways to do that... 
                //                List<int> neighborIndicesIgnoreTopo = new List<int>();
                //                List<int> neighborIndicesUseTopo = new List<int>();
                List<int> neighborIndices2ndGenOnly = new List<int>();
                //                neighborIndicesIgnoreTopo.AddRange(getNeighborsBrokenIgnoresTopology(i));
                //                neighborIndicesUseTopo.AddRange(getNeighborsToplogyAware(i));
                neighborIndices2ndGenOnly.AddRange(getOnlyNthGeneration(2, i));
                List<int> indicesAddedByTopo = new List<int>();
                // serach for the largest and smallest local curvatures using forward differencing. 
                // these are the principal curvatures. 
                double largestLocalCurvature;
                double smallestLocalCurvature;
                largestLocalCurvature = -1000.0;
                smallestLocalCurvature = 1000.0;
                foreach (int neighborIndex in neighborIndices2ndGenOnly)
                {
                    // color the neighbors green. 
                    //pendingColorChanges.Add(new ColorChange(neighborIndex, Color.Green));
                    // draw the neighbor normal 
                    double dx = 0;
                    double dz = 0;
                    calculateDxAndDz(i, neighborIndex, ref dx, ref dz);
                    double curvature = -dz / dx;
                    /*if (curvature < 0 )
                    {
                        addColorChangeToList(neighborIndex, Color.Red);
                    }
                    else
                    {
                        addColorChangeToList(neighborIndex, Color.Blue); 
                    }*/
                    //LogToRhino.writeToRhino("local curvature [" + i + "," + neighborIndex + "] is " + curvature); 
                    smallestLocalCurvature = Math.Min(smallestLocalCurvature, curvature);
                    largestLocalCurvature = Math.Max(largestLocalCurvature, curvature);
                }
                double meanCurvature = (smallestLocalCurvature + largestLocalCurvature) / 2.0;
                //LogToRhino.writeToRhino("mean curvature " + meanCurvature); 
                signedMeanCurvatures[i] = meanCurvature;
            }
            curvatureIsCalcuated = true;

        }

        private void calculateDxAndDz(int vertexIndex, int neighborVertexIndex, ref double dx, ref double dz)
        {
            Point3d theVertex = housingMesh.Vertices[vertexIndex];
            Point3d neighborVertex = housingMesh.Vertices[neighborVertexIndex];
            Vector3d vertexNormal = housingMesh.Normals[vertexIndex];
            vertexNormal.Unitize();
            // compute the rate of change from here to neighbor as a forward difference. 
            // using dx/dz in theVertex's local coordinate system.  
            Vector3d vertexToNeighbor = neighborVertex - theVertex;
            Vector3d localY = Vector3d.CrossProduct(vertexToNeighbor, vertexNormal);
            localY.Unitize();
            Vector3d localX = Vector3d.CrossProduct(vertexNormal, localY);
            //doc.Objects.AddLine(new Line(theVertex, localX, 5.0));
            //doc.Objects.AddLine(new Line(theVertex, localY, 5.0));
            //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 5.0));
            dx = vertexToNeighbor * localX;
            dz = vertexToNeighbor * vertexNormal;
        }

        private List<int> getNeighborsBrokenIgnoresTopology(int vertex)
        {
            List<int> neighbors = new List<int>();
            int[] neighborVertices = housingMesh.Vertices.GetConnectedVertices(vertex);
            neighbors.AddRange(neighborVertices);
            return neighbors;
        }


        private HashSet<int> getOnlyNthGeneration(int n, int startingVertex)
        {
            HashSet<int> nthGeneration = new HashSet<int>();
            HashSet<int> everyone = new HashSet<int>();
            HashSet<int> prevGeneration = new HashSet<int>();
            HashSet<int> nonUniqueNeighbors = new HashSet<int>();
            nthGeneration.UnionWith(makeHashSetOf(getNeighborsToplogyAware(startingVertex)));
            for (int generation = 1; generation <= n; generation++)
            {
                everyone.UnionWith(nthGeneration);
                prevGeneration.Clear();
                prevGeneration.UnionWith(nthGeneration);
                nthGeneration.Clear();
                foreach (int previous in prevGeneration)
                {
                    nonUniqueNeighbors = makeHashSetOf(getNeighborsToplogyAware(previous));
                    nthGeneration.UnionWith(nonUniqueNeighbors);
                }
                nthGeneration.ExceptWith(everyone);
            }
            return nthGeneration;
        }

        private HashSet<int> makeHashSetOf(List<int> list)
        {
            HashSet<int> returnValue = new HashSet<int>();
            foreach (int i in list)
            {
                returnValue.Add(i);
            }
            return returnValue;
        }

        private void addListToHashSet(List<int> list, ref HashSet<int> nthGeneration)
        {
            foreach (int member in list)
            {
                nthGeneration.Add(member);
            }
        }

        private List<int> getNeighborsToplogyAware(int vertex)
        {
            List<int> neighbors = new List<int>();
            Rhino.Geometry.Collections.MeshTopologyVertexList mtvl = housingMesh.TopologyVertices;
            int vertexTopo = mtvl.TopologyVertexIndex(vertex);
            int[] neighborsTopo = mtvl.ConnectedTopologyVertices(vertexTopo);
            for (int neighborTopoIndex = 0; neighborTopoIndex < neighborsTopo.Length; neighborTopoIndex++)
            {
                int[] neighborVertices = mtvl.MeshVertexIndices(neighborsTopo[neighborTopoIndex]);
                for (int neighborIndex = 0; neighborIndex < neighborVertices.Length; neighborIndex++)
                {
                    neighbors.Add(neighborVertices[neighborIndex]);
                }
            }
            return neighbors;
        }


        internal void colorNthGeneration(int n)
        {
            for (int i = 4; i < housingMesh.Vertices.Count; i += Constants.curvatureVertexIncrement)
            {
                HashSet<int> nthGeneration = getOnlyNthGeneration(n, i);
                foreach (int index in nthGeneration)
                {
                    vertexColors[index] = Color.HotPink;
                }
                vertexColors[i] = Color.Crimson;
            }
        }



        internal void colorCurvatureByThisRange(double min, double max)
        {
            Constants.minCurvatureToColor = min;
            Constants.maxCurvatureToColor = max;
            colorCurvature();
        }

        internal void colorCurvature()
        {
            if (curvatureIsCalcuated)
            {
                double maxc = -100000;
                double minc = 100000;
                for (int i = 0; i < housingMesh.Vertices.Count; i += Constants.curvatureVertexIncrement)
                {
                    double curvature = signedMeanCurvatures[i];
                    maxc = Math.Max(maxc, curvature);
                    minc = Math.Min(minc, curvature);
                }
                RhinoLog.debug("max curvature " + maxc + " min curvature " + minc);
                double range = maxc - minc;
                if (range != 0)
                {
                    double scaleTo360Range = 360 / range;
                    for (int i = 0; i < housingMesh.Vertices.Count; i += Constants.curvatureVertexIncrement)
                    {
                        double curvature = signedMeanCurvatures[i];
                        if (curvature > minc && curvature < maxc)
                        {
                            // three ways to color  by curvature... 
                            // 1.  
                            // vertexColors[i] = ColorByIncrement(curvature, 0.5);
                            // 2.  
                            vertexColors[i] = colorByRange(curvature, Constants.minCurvatureToColor, Constants.maxCurvatureToColor); // if curvature is defined as only second gen topological neighbors using 
                                                                                                                                     // forward differencing only then 0.7 to 1.5 is a generous range for feature identification on some models. 
                                                                                                                                     // 3. 
                                                                                                                                     //int colorindex = (int)((curvature - minc) * scaleTo360Range);
                                                                                                                                     //  vertexColors[i] = RhinoLog.HsvtoColor(colorindex, 0.6, 0.9);
                        }
                        else
                        {
                            vertexColors[i] = Color.Black;
                            Point3d point = housingMesh.Vertices[i];
                            Vector3d normal = housingMesh.Normals[i];
                            normal.Unitize();
                            //                            RhinoLog.DrawCylinder(point, normal, 1.0, 0.2, Color.HotPink, doc); 
                        }
                    }
                }
            }
        }

        private Color ColorByIncrement(double value, double increment)
        {
            // let's do three bands. 
            double bandDbl = Math.Abs(value / increment);
            int globalband = (int)Math.Truncate(bandDbl);
            int band = globalband % 4;
            Color[] bandColors = new Color[4] { Color.HotPink, Color.Khaki, Color.Green, Color.Cornsilk };
            return bandColors[band];

        }

        private Color colorByRange(double value, double low, double high)
        {
            if (value < high && value > low)
            {
                return Color.Red;
            }
            else if (value > high)
            {
                return Color.Gold;
            }
            else if (value < low)
            {
                return Color.White;
            }
            else
            {
                return Color.Black;
            }
        }

        internal void colorFeatures()
        {
            if (featuresAreFound)
            {
                // set the vertex colors for the features.  
                foreach (SurfaceFeature sf in surfaceFeatures)
                {
                    vertexColors[sf.indexIntoTheMeshVertexList] = sf.color;
                    Point3d point = housingMesh.Vertices[sf.indexIntoTheMeshVertexList];
                    Vector3d normal = housingMesh.Normals[sf.indexIntoTheMeshVertexList];
                    //RhinoLog.DrawCylinder(point, normal, 1.0, 0.2, Color.GreenYellow, doc);
                }
                // make the color changes visible. 
            }
        }

        /// <summary>
        /// tell me how you want the vertices colored.  
        /// </summary>
        /// <returns></returns>
        internal override Color[] colorize()
        {
            return vertexColors; 
        }
    }
}