using Rhino.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino;
using System.Drawing;

namespace RhinoTweak
{
    class HousingMesh
    {
        private List<WidgetSite> widgetSites;
        private List<SurfaceFeature> surfaceFeatures;
        private List<HousingVertex> housingVertices;
        private Mesh theMesh;
        private System.Guid meshToManage;
        private RhinoDoc doc;
        /// <summary>
        ///  indexed by vertex indices.  
        /// </summary>
        private double[] signedMeanCurvatures;
        /// <summary>
        ///  indexed by vertex incides. 
        /// </summary>
        private Color[] vertexColors; 
        Boolean curvatureIsCalcuated = false;
        Boolean meshGeometryHasChanged = false;
        Boolean featuresAreFound = false; 
        private double curvatureFeatureThreshold = 0.21;
        private double featureDistanceMatchThreshold = 0.25;
        private double featureAngleMatchThreshold = 3.2;

        public HousingMesh (Mesh m, Guid m2manage, RhinoDoc doc)
        {
            widgetSites = new List<WidgetSite>();
            surfaceFeatures = new List<SurfaceFeature>();
            housingVertices = new List<HousingVertex>();
            this.doc = doc;

            // deep copy or this? mdj 
            theMesh = m.DuplicateMesh();
            meshToManage = m2manage;

            signedMeanCurvatures = new double[m.Vertices.Count];
            curvatureIsCalcuated = false;

            // initialize vertex colors.  It's handy if all of them have
            // an assigned color.  then we can go in later and willy nilly
            // assign anyone a new color using mesh.VertexColors.SetColor (index,color). 
            vertexColors = new Color[m.Vertices.Count];
            for (int i = 0; i < m.Vertices.Count; i++)
            {
                vertexColors[i] = Color.Bisque; 
            }

        }

        internal void findWidgetSites(List<WidgetBlank> widgetBlanks)
        {
            List<SurfaceFeature> featuresToMatch = new List<SurfaceFeature>();
            // go through each feature and see if it and it's neighbors match 
            // a widget 
            foreach (SurfaceFeature sf in surfaceFeatures)
            {
                featuresToMatch.Clear();
                featuresToMatch.Add(sf);
                foreach (WidgetBlank widget in widgetBlanks)
                {
                    // see if our feature type matches any of those in the widget. 
                    // widget features are ordered.  So we match on the first feature
                    // first.  
                    if (matchUpFeaturesInOrder(featuresToMatch, widget))
                    {
                        // see if there's a feature nearby of the right type 
                        // and in the right place. 
                        // TODO: just go through a neighborhood of other features.  not all of them. 
                        foreach (SurfaceFeature otherSF in surfaceFeatures)
                        {
                            featuresToMatch.Add(otherSF);
                            if (matchUpFeaturesInOrder(featuresToMatch, widget))
                            {
                                foreach (SurfaceFeature thirdSF in surfaceFeatures)
                                {
                                    featuresToMatch.Add(thirdSF);
                                    if (matchUpFeaturesInOrder(featuresToMatch, widget))
                                    {
                                        // we have a match.  rejoice. 
                                        RhinoLog.write("we have a match for " + widget.name);
                                        Point3d p0 = theMesh.Vertices[featuresToMatch[0].indexIntoTheMeshVertexList];
                                        Point3d p1 = theMesh.Vertices[featuresToMatch[1].indexIntoTheMeshVertexList];
                                        Point3d p2 = theMesh.Vertices[featuresToMatch[2].indexIntoTheMeshVertexList];
                                        RhinoLog.DrawCylinder(p0, p1, 0.2, Color.Red, doc);
                                        RhinoLog.DrawCylinder(p1, p2, 0.1, Color.Blue, doc);
                                        RhinoLog.DrawCylinder(p0, p2, 0.6, Color.Green, doc);

                                    }
                                    featuresToMatch.Remove(thirdSF);
                                }
                            }
                            featuresToMatch.Remove(otherSF);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// so the rationale here is to do as little work as possible for each potential matching.
        /// The logic for this is going to be hairy, so I wanted to surpess it in the main feature 
        /// match routine because that also has some hairy logic related to which vertices to check. 
        /// </summary>
        /// <param name="surfaceFeatures">a list of features to check.  Behavior depends on the length of this list.</param>
        /// <param name="widget">the widget blank to test against.</param>
        /// <returns></returns>
        private bool matchUpFeaturesInOrder(List<SurfaceFeature> surfaceFeatures, WidgetBlank widget)
        {
            Boolean couldBeAmatch = false; 

            if (surfaceFeatures.Count >= 1)
            {
                // check type of feature 0. 
                couldBeAmatch = widget.nthFeatureIsType(0,surfaceFeatures[0]);
                RhinoLog.debug("matched type"); 
            } 
            if (surfaceFeatures.Count >= 2 && couldBeAmatch)
            {
                // check type of feature 1. 
                couldBeAmatch = couldBeAmatch && widget.nthFeatureIsType(1,surfaceFeatures[1]);
                // check distances. 
                couldBeAmatch = couldBeAmatch && checkDistances(0, 1,surfaceFeatures,widget); 
            }
            if (surfaceFeatures.Count >= 3 && couldBeAmatch)
            {
                // check type of feature 2 
                couldBeAmatch = couldBeAmatch && widget.nthFeatureIsType(2,surfaceFeatures[2]);
                // check distances. 
                couldBeAmatch = couldBeAmatch && checkDistances(0, 2, surfaceFeatures, widget);
                if (couldBeAmatch)
                {
                    double distance1to2 =
                        getPoint3DForIndex(surfaceFeatures[1].indexIntoTheMeshVertexList).DistanceTo(
                            getPoint3DForIndex(surfaceFeatures[2].indexIntoTheMeshVertexList));
                    double widgetdistance1to2 =
                        widget.distanceFromFeature(1, 2);
                }
                couldBeAmatch = couldBeAmatch && checkDistances(1, 2, surfaceFeatures, widget);
                // check angles.  
                if (couldBeAmatch)
                {
                    couldBeAmatch = couldBeAmatch && checkAngles(0, 1, 2, surfaceFeatures, widget);
                    couldBeAmatch = couldBeAmatch && checkAngles(1, 0, 2, surfaceFeatures, widget);                    
                }
            }
            return (couldBeAmatch);
        }

        /// <summary>
        /// compute the angle formed by v1 to v2 and v1 to v3.  see if that matches the angle between 
        /// features v1 to v2 and v1 to v3 in the widget.  
        /// </summary>
        /// <param name="surfaceFeatureIndex1">the vertex at the angle</param>
        /// <param name="sfi2"></param>
        /// <param name="sfi3"></param>
        /// <param name="surfaceFeatures"></param>
        /// <param name="widget"></param>
        /// <returns></returns>
        private bool checkAngles(int surfaceFeatureIndex1, int sfi2, int sfi3, List<SurfaceFeature> surfaceFeatures, WidgetBlank widget)
        {
            Point3d p1 = getPoint3DForIndex(surfaceFeatures[surfaceFeatureIndex1].indexIntoTheMeshVertexList);
            Point3d p2 = getPoint3DForIndex(surfaceFeatures[sfi2].indexIntoTheMeshVertexList);
            Point3d p3 = getPoint3DForIndex(surfaceFeatures[sfi3].indexIntoTheMeshVertexList); 
            Vector3d v1tov2 = p2-p1;
            Vector3d v1tov3 = p3-p1;
            // angle is in degrees. 
            double angleOnHousing = myAngleFinder(v1tov2, v1tov3); 
            double angleOnWidget = widget.angleBetween(surfaceFeatureIndex1, sfi2, sfi3);
            return (anglesAreCloseEnough(angleOnHousing, widget.angleBetween(surfaceFeatureIndex1, sfi2, sfi3))); 
        }

        private double myAngleFinder(Vector3d v1tov2, Vector3d v1tov3)
        {
            double dotProduct = v1tov2 * v1tov3;
            double productOfLengths = v1tov3.Length * v1tov2.Length;
            double angleRadians = Math.Acos(dotProduct / productOfLengths);
            double angle = angleRadians * (180 / Math.PI); 
            return angle; 
        }


        private bool anglesAreCloseEnough(double angleOnHousing, double p)
        {
            return (Math.Abs(angleOnHousing - p) < featureAngleMatchThreshold); 
        }

        /// <summary>
        /// see if the distance between vertices v1 and v2 in surface features list are 
        /// of an appropriate distance to match featuers v1 and v2 of the widget. 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="surfaceFeatures"></param>
        /// <param name="widget"></param>
        /// <returns></returns>
        private bool checkDistances(int v1, int v2, List<SurfaceFeature> surfaceFeatures, WidgetBlank widget)
        {
            double distanceBetweenFeatures =
                   getPoint3DForIndex(surfaceFeatures[v1].indexIntoTheMeshVertexList)
                        .DistanceTo(getPoint3DForIndex(surfaceFeatures[v2].indexIntoTheMeshVertexList));
            double distanceOnObject =
                widget.distanceFromFeature(v1, v2);
            RhinoLog.debug("for features "+v1+" and " +v2 +" distance in mesh " + distanceBetweenFeatures + " distance in widget " + distanceOnObject); 
            return (distancesCloseEnough(distanceBetweenFeatures, widget.distanceFromFeature(v1, v2))); 
        }

        private bool distancesCloseEnough(double distanceBetweenFeatures, double distanceBetweenFeaturesOnWidget)
        {
            return (Math.Abs(distanceBetweenFeatures - distanceBetweenFeaturesOnWidget) < featureDistanceMatchThreshold); 
        }

        private Point3d getPoint3DForIndex(int index)
        {
            return (theMesh.Vertices[index]);
        }

        internal void findFeatures()
        {
            // make sure the curvature is calculated. 
            if (!curvatureIsCalcuated && !meshGeometryHasChanged)
            {
                //RhinoLog.write("calculating curvature...");
                calculateSignedMeanCurvature();
                //RhinoLog.write("done"); 
            }
            // find the features.  First look at curvature. 
            // large magnitude curvature means you are a feature. 
            for (int i = 0; i < theMesh.Vertices.Count; i++)
            {
                if (signedMeanCurvatures[i] > curvatureFeatureThreshold )
                {
                    // we have an outie (point poking out with positive curvature) surface feature. 
                    surfaceFeatures.Add(new SurfaceFeature(i,SurfaceFeature.featureType.outie));
                    surfaceFeatures.Last().color = Color.Firebrick;
                }
                if (signedMeanCurvatures[i] < -curvatureFeatureThreshold)
                {
                    // we have an innie 
                    surfaceFeatures.Add(new SurfaceFeature(i, SurfaceFeature.featureType.innie));
                    surfaceFeatures.Last().color = Color.Purple; 
                }
            }
            featuresAreFound = true; 
            RhinoLog.write("found " + surfaceFeatures.Count + " surface features"); 
        }

        private void calculateSignedMeanCurvature()
        {
            for (int i = 0; i < theMesh.Vertices.Count; i++)
            {
                Point3d theVertex = theMesh.Vertices[i];
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
                 double largestLocalCurvature;
                 double smallestLocalCurvature;
                largestLocalCurvature = -1000.0;
                smallestLocalCurvature = 1000.0;
                foreach (int neighborIndex in neighborIndices)
                {
                    // color the neighbors green. 
                    //pendingColorChanges.Add(new ColorChange(neighborIndex, Color.Green));
                    // draw the neighbor normal 
                    Point3d neighborVertex = theMesh.Vertices[neighborIndex];
                    // compute the rate of change from here to neighbor as a forward difference. 
                    // using dx/dz in theVertex's local coordinate system.  
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
            }
            curvatureIsCalcuated = true; 

        }

        internal void colorFeatures()
        {
            if (featuresAreFound)
            {
                // set the vertex colors for the features.  
                foreach (SurfaceFeature sf in surfaceFeatures)
                {
                    vertexColors[sf.indexIntoTheMeshVertexList] = sf.color; 
                }
                // make the color changes visible. 
                redrawSurfaceColors(); 
            }
        }

        private void redrawSurfaceColors()
        {
            // make a new mesh. 
            Mesh newMesh = theMesh.DuplicateMesh();
            newMesh.VertexColors.Clear(); 
            for (int i = 0; i < newMesh.Vertices.Count; i++)
            {
                newMesh.VertexColors.SetColor(i, vertexColors[i]); 
            }
            newMesh.FaceNormals.ComputeFaceNormals(); 
            doc.Objects.Replace( meshToManage, newMesh);
            doc.Views.Redraw(); 
            theMesh = newMesh.DuplicateMesh(); 
        }
    }
}
