using Rhino;
using Rhino.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class vertexColorManager
    {

        public ArrayList pendingColorChanges;
        System.Guid whichMeshToManage; 

        public vertexColorManager (Guid idOfMeshToManage)
        {
            pendingColorChanges = new ArrayList();
            whichMeshToManage = idOfMeshToManage; 
        }


        private class ColorChange : Object
        {
            public int vertex
            {
                get; set;
            }
            public Color color
            {
                get; set;
            }
            public ColorChange(int vertex, Color color)
            {
                this.vertex = vertex;
                this.color = color;
            }
        }

        public void addColorChangeToList (int vertex, Color color)
        {
            pendingColorChanges.Add(new ColorChange(vertex, color)); 
        }

        public void resetPendingChanges ()
        {
            pendingColorChanges.Clear(); 
        }

        public Mesh makeTheChange(Mesh oldMesh,RhinoDoc doc)
        {
            Mesh newMesh = oldMesh.DuplicateMesh();
            newMesh.VertexColors.Clear();
            // color everything light grey. 
            for (int i = 0; i < newMesh.Vertices.Count; i++)
            {
                newMesh.VertexColors.Add(Color.Bisque);
            }

            // now make the changes. 
            foreach (ColorChange cc in pendingColorChanges)
            {
                try
                {
                    newMesh.VertexColors.SetColor(cc.vertex, cc.color);
                }
                catch (Exception e)
                {
//                    LogToRhino.writeToRhino("exceptoin in change colors: " + e.Message);
//                    LogToRhino.writeToRhino("color list has " + newMesh.VertexColors.Count + " entries but we are adding " +
//                        cc.vertex);
                }

                //writeToRhino("actually changing vertex " + cc.vertex + " to " +
                //   cc.color.ToString()); 
            }
            doc.Objects.Replace(whichMeshToManage, newMesh);
            doc.Views.Redraw(); 
            return newMesh;
        }


        private double dotProduct(Vector3d a, Vector3d b)
        {
            return (a.X * b.X + a.Y * b.Y + a.Z * b.Z);
        }

        public Mesh colorByMeanCurvature(Mesh theMesh, RhinoDoc doc)
        {
            pendingColorChanges.Clear();
            int vertices = theMesh.Vertices.Count;
            double smallestLocalCurvature = 1000.0;
            double largestLocalCurvature = -10000.0;
            double smallestMeanCurvature = 10000.0;
            double largestMeanCurvature = -10000.0;
            double coneTipThreshold = 0.19;  
            // won't know how to compute the color scale until later, so save the curvatures. 
            double[] signedMeanCurvatures = new double[vertices];
            for (int i = 0; i < theMesh.Vertices.Count; i ++)
            {
                Point3d theVertex = theMesh.Vertices[i];
                //pendingColorChanges.Add(new ColorChange(i,Color.White));
                // get the vertex normal, draw it. 
                Vector3d vertexNormal = theMesh.Normals[i];
                vertexNormal.Unitize();
                // draw the vertex normal. 
                //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 10.0));

                // get the neighbors. 
                ArrayList neighborIndices = new ArrayList();
                neighborIndices.AddRange(theMesh.Vertices.GetConnectedVertices(i));
                neighborIndices.Remove(i);
                ArrayList neighborNeighborIndices = new ArrayList();
                // serach for the largest and smallest local curvatures using forward differencing. 
                // these are the principal curvatures. 
                largestLocalCurvature = -1000.0;
                smallestLocalCurvature = 1000.0; 
                foreach (int neighborIndex in neighborIndices)
                {
                    // color them green. 
                    //pendingColorChanges.Add(new ColorChange(neighborIndex, Color.Green));
                    // draw the neighbor normal 
                    Point3d neighborVertex = theMesh.Vertices[neighborIndex];
                    Vector3d neighborVertexNormal = theMesh.Normals[neighborIndex];
                    // compute the rate of change from here to neighbor as a forward difference. 
                    Vector3d vertexToNeighbor = neighborVertex - theVertex;
                    Vector3d localY = Vector3d.CrossProduct(vertexToNeighbor, vertexNormal);
                    localY.Unitize();
                    Vector3d localX = Vector3d.CrossProduct(vertexNormal, localY);
                    //doc.Objects.AddLine(new Line(theVertex, localX, 5.0));
                    //doc.Objects.AddLine(new Line(theVertex, localY, 5.0));
                    //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 5.0));
                    double dx = vertexToNeighbor * localX;
                    double dz = vertexToNeighbor * vertexNormal;
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
                smallestMeanCurvature = Math.Min(smallestMeanCurvature, meanCurvature);
                if (meanCurvature < coneTipThreshold)
                    largestMeanCurvature = Math.Max(largestMeanCurvature, meanCurvature);
                }
            // scale those colors so we can see them. 
            double curvatureRange =  largestMeanCurvature - smallestMeanCurvature;
            double colorRangeScalar = 1.0;
            if (curvatureRange != 0.0)
            {
                colorRangeScalar = 255.0 / curvatureRange;
            }
//            LogToRhino.writeToRhino("largest mean curvature " + largestMeanCurvature+ " smallest mean curvature " + smallestMeanCurvature);
            for (int i = 0; i < theMesh.Vertices.Count; i++)
            {
                double curvature = signedMeanCurvatures[i];
                double scaledCurvature = (curvature - smallestMeanCurvature) * colorRangeScalar;
                //LogToRhino.writeToRhino("curvature " + curvature + " scaled as a color " + scaledCurvature); 
                // mark the cone tips special. 
                if (curvature > coneTipThreshold)
                {
                    pendingColorChanges.Add(new ColorChange(i, Color.Blue));
                    //indicesOfConeTips.Add(i);
                }
                else
                {
                    int redComponent = (int)(scaledCurvature);
                    int greenComponent = (int)scaledCurvature;
                    int blueComponent = (int)(scaledCurvature);
                    //LogToRhino.writeToRhino("scaled red = " + redComponent + " original value " + curvature); 
                    pendingColorChanges.Add(new ColorChange(i, Color.FromArgb(redComponent, greenComponent, blueComponent)));
                }
            }
            //LogToRhino.writeToRhino("found " + indicesOfConeTips.Count + " cone tips");
            return makeTheChange(theMesh, doc);
        }


        public Mesh colorByDotProductOfVertexNormals(Mesh theMesh, RhinoDoc doc)
        {
            // take every 200th vertex and change it's color to blue. 
            // ... can't see that because the blue gets drowned. 
            // change all it's neighbors to green. 
            // change the neighbors of the neighbors to yellow. 
            pendingColorChanges.Clear();
            double coneTipThreshhold = 0.7;
            int vertices = theMesh.Vertices.Count;
            double smallestDotProduct = 1000.0;
            double largestDotProduct = 0.0;
            // won't know how to compute the color scale until later, so save the dot products. 
            double[] averageDotProducts = new double[vertices];
            for (int i = 0; i < theMesh.Vertices.Count; i += 1)
            {
                Point3d theVertex = theMesh.Vertices[i];
                //pendingColorChanges.Add(new ColorChange(i,Color.White));
                // get the vertex normal, draw it. 
                Vector3d vertexNormal = theMesh.Normals[i];
                // draw the vertex normal. 
                //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 10.0));

                // get the neighbors. 
                ArrayList neighborIndices = new ArrayList();
                neighborIndices.AddRange(theMesh.Vertices.GetConnectedVertices(i));
                neighborIndices.Remove(i);
                ArrayList neighborNeighborIndices = new ArrayList();
                double runningDotProductOfVertexNormals = 0.0;
                foreach (int neighborIndex in neighborIndices)
                {
                    // color them green. 
                    //pendingColorChanges.Add(new ColorChange(neighborIndex, Color.Green));
                    // draw the neighbor normal 
                    Point3d neighborVertex = theMesh.Vertices[neighborIndex];
                    Vector3d neighborVertexNormal = theMesh.Normals[neighborIndex];
                    // compute the dot product of the normal and the neighbor normal. 
                    runningDotProductOfVertexNormals += dotProduct(neighborVertexNormal, vertexNormal);

                    //doc.Objects.AddLine(new Line(neighborVertex, neighborVertexNormal, 5.0));    
                    // and neighbor's neighbors to a list. 
                    foreach (int neighborNeighborIndex in theMesh.Vertices.GetConnectedVertices(neighborIndex))
                    {
                        // ... only if it's not already in the neighbor list or the neighbor neighbor neighbor list. 
                        if (!(neighborIndices.Contains(neighborNeighborIndex) &&
                            !(neighborNeighborIndices.Contains(neighborNeighborIndex) &&
                            (neighborNeighborIndex != i))))
                        {
                            neighborNeighborIndices.Add(neighborNeighborIndex);
                        }
                    }
                }
                //foreach (int neighborIndex in neighborNeighborIndices)
                //{
                //    pendingColorChanges.Add(new ColorChange(neighborIndex, Color.Yellow)); 
                //}
                double averageDotProduct = runningDotProductOfVertexNormals / neighborIndices.Count;
                // tuck the average dot product away for later. 
                averageDotProducts[i] = averageDotProduct;
                if (averageDotProduct > largestDotProduct)
                {
                    largestDotProduct = averageDotProduct;
                }
                if (averageDotProduct < smallestDotProduct && averageDotProduct > coneTipThreshhold)
                {
                    smallestDotProduct = averageDotProduct;
                }
            }
            // scale those colors so we can see them. 
            double dotProductRange = largestDotProduct - smallestDotProduct;
            double redRangeScalar = 1.0;
            if (dotProductRange != 0.0)
            {
                redRangeScalar = 255.0 / dotProductRange;
            }
//            LogToRhino.writeToRhino("largest dot product " + largestDotProduct + " smallest dot product " + smallestDotProduct);
//            LogToRhino.writeToRhino("dot product range " + dotProductRange);
//            LogToRhino.writeToRhino("dot product scalar " + redRangeScalar);
            for (int i = 0; i < theMesh.Vertices.Count; i++)
            {
                double averageDotProduct = averageDotProducts[i];
                double scaledAverageDotProduct = (averageDotProduct - smallestDotProduct) * redRangeScalar;
                // mark the cone tips special. 
                if (averageDotProduct < coneTipThreshhold)
                {
                    pendingColorChanges.Add(new ColorChange(i, Color.Blue));
                    //indicesOfConeTips.Add(i);
                }
                else
                {
                    int redComponent = 255 - (int)(scaledAverageDotProduct / 60);
                    int greenComponent = (int)scaledAverageDotProduct;
                    int blueComponent = 0 + (int)(scaledAverageDotProduct / 1);
                    // writeToRhino("scaled red = " + redComponent + " original value " + averageDotProduct); 
                    pendingColorChanges.Add(new ColorChange(i, Color.FromArgb(redComponent, greenComponent, blueComponent)));
                }
            }
            //LogToRhino.writeToRhino("found " + indicesOfConeTips.Count + " cone tips");
            return makeTheChange(theMesh,doc);
        }
    }
}
