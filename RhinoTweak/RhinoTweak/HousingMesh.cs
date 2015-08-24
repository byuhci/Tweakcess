using Rhino.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino;
using System.Drawing;
using Rhino.DocObjects;

namespace RhinoTweak
{
    class HousingMesh
    {
        private List<WidgetPlacement> WidgetPlacements;
        private HashSet<SurfaceFeature> surfaceFeatures;
        private List<HousingVertex> housingVertices;
        private Mesh housingMesh;
        private System.Guid housingMeshGuid;
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
        private double curvatureFeatureThresholdLow = 0.7;
        private double curvatureFeatureThresholdHigh = 1.5;
        // this was a good set of paramters for feature identification with the broken 
        // curvature estimation that wasn't topology aware. 
        // private double curvatureFeatureThresholdLow = 0.20;
        //private double curvatureFeatureThresholdHigh = 0.26;

        private double featureDistanceMatchThreshold = 0.85;
        private double featureAngleMatchThreshold = 3.2;
        private int curvatureVertexIncrement = 1;
        private double minCurvatureToColor = 0.9;
        private double maxCurvatureToColor = 1.5;
        private double flatThresholdForDz = 0.09;
        private int minFlatsToBeConsideredNotAFeature = 2;

        public HousingMesh (Mesh m, Guid m2manage, RhinoDoc doc)
        {
            WidgetPlacements = new List<WidgetPlacement>();
            surfaceFeatures = new HashSet<SurfaceFeature>();
            housingVertices = new List<HousingVertex>();
            this.doc = doc;

            // deep copy or this? mdj 
            housingMesh = m.DuplicateMesh();
            housingMeshGuid = m2manage;

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

        #region insert a widget and a placement site after finding placement sites

        public void placeWidgets ()
        {
            foreach (WidgetPlacement placement in WidgetPlacements)
            {
                // load up and place the slug.  
                MeshObject slugMeshObject; 
                 slugMeshObject = readInMesh(placement, WidgetBlank.pieces.slug); 
                Mesh slugMesh = moveMeshObjectIntoPlace(slugMeshObject, placement);
                // do the boolean difference. 
                List<Mesh> housingMeshList = new List<Mesh>();
                housingMeshList.Add(housingMesh); 
                List<Mesh> slugMeshList = new List<Mesh>();
                slugMeshList.Add(slugMesh); 
                Mesh[] differencePieces =
                    Mesh.CreateBooleanDifference(housingMeshList, slugMeshList);
                housingMesh = changeAMesh(differencePieces[0], housingMeshGuid);
                doc.Objects.Delete(slugMeshObject.Id, true);
                doc.Views.Redraw();

                // load up and place the bracket. 
                MeshObject bracketMeshObject =
                   readInMesh(placement, WidgetBlank.pieces.bracket);
                Mesh bracketMesh =
                    moveMeshObjectIntoPlace(bracketMeshObject, placement); 
                
            }
        }

        private MeshObject readInMesh(WidgetPlacement placement, WidgetBlank.pieces whichPiece)
        {
            RhinoLog.debug("importing widget " + placement.widget.kind);
            placement.widget.importSTLFile(whichPiece);
            // the guid is the guid of the last mesh in the list. 
            List<RhinoObject> stuffInTheDoc = new List<RhinoObject>();
            stuffInTheDoc.AddRange(doc.Objects.GetSelectedObjects(false, false));
            if (stuffInTheDoc.Count != 1)
            {
                RhinoLog.error("should have imported exactly one mesh for the widget, file not found?");
                // but it wouldn't be hard to just loop over all the meshes and 
                // do whatever to each in turn.  
            }
            MeshObject widgetMeshObject = (MeshObject)stuffInTheDoc.First();
            // unselect what we just read in. 
            stuffInTheDoc.First().Select(false);
            return widgetMeshObject; 
        }

        private Mesh moveMeshObjectIntoPlace(MeshObject widgetMeshObject, WidgetPlacement placement)
        {
            Mesh widgetMesh = widgetMeshObject.MeshGeometry;
            Guid widgetMeshGUID = widgetMeshObject.Id;
            widgetMesh = colorTheMesh(widgetMesh, widgetMeshGUID, Color.Fuchsia);
            // rotate mesh to match the placement normal.
            // ASSUME when the widget is imported it's aligned so the Z axis 
            //   is to be aligned with placement normal.   
            // ASSUME that the line from feature 0 to feature 1 is aligned 
            //    with the X axis. 
            Vector3d axisOfRotationInZ = Vector3d.CrossProduct(Vector3d.ZAxis, placement.normal);
            double rotationAngleDegreees =
                myAngleFinder(Vector3d.ZAxis, placement.normal);
            widgetMesh = rotate(widgetMesh, rotationAngleDegreees, axisOfRotationInZ, Point3d.Origin, widgetMeshGUID);

            // ASSUME that the widget x axis is aligned with the world x axis on import. 
            Vector3d rotatedWidgetXAxis = Vector3d.XAxis;
            double rotationAngleRadians = rotationAngleDegreees * (Math.PI / 180.0); 
            // and we just rotated the widget so we have to rotate it's local x axis.  
            rotatedWidgetXAxis.Rotate(rotationAngleRadians, axisOfRotationInZ); 
            Vector3d axisOfRotationInX = Vector3d.CrossProduct(rotatedWidgetXAxis, placement.xaxis);
            double rotationAngleDegreesX =
                myAngleFinder(rotatedWidgetXAxis, placement.xaxis);
            widgetMesh = rotate(widgetMesh, rotationAngleDegreesX, axisOfRotationInX, Point3d.Origin, widgetMeshGUID); 

            // translate it to match the centroid. 
            // ASSUME when the widget is imported it's placed so that the 
            //    location at the world origin is translated to the 
            //    placement centroid.  
            // boolean ops to make it part of the mesh.  
            Vector3d translationVector = placement.centroid - Point3d.Origin;
            widgetMesh = translate(widgetMesh, translationVector, widgetMeshGUID);
            return widgetMesh;
        }

        private Mesh colorTheMesh(Mesh aMesh, Guid meshGUID, Color color)
        {
            Color[] listofcolors = new Color[aMesh.Vertices.Count]; 
            // set each vertex to the color. 
            for (int i =0; i < aMesh.Vertices.Count; i++)
            {
                listofcolors[i] = color;  
            }
            // make the change and pass back the new mesh. 
            return (redrawSurfaceColors(aMesh,meshGUID, listofcolors)); 
        }

        #endregion

        #region finding widget placement sites on the surface after finding features. 
        /// <summary>
        ///  go through this list of widget blanks and find all the widget placements
        /// that match those blanks by analyzing the surface features found on this 
        /// mesh.  
        /// Stores placements in the widgetplacements member.  
        /// </summary>
        /// <param name="widgetBlanks"></param>
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
                    // first.  the behavior of matchupfeatures in order depends 
                    // on the length of the "featuresToMatch" list fyi.  
                    // first feature matches? 
                    if (matchUpFeaturesInOrder(featuresToMatch, widget))
                    {
                        // yes.  
                        Point3d firstFeaturePoint =
                            housingMesh.Vertices[sf.indexIntoTheMeshVertexList];
                        //RhinoLog.DrawSphere(firstFeaturePoint, 0.5, Color.ForestGreen, doc); 
                        // see if there's a feature nearby of the right type 
                        // and in the right place. 
                        // TODO: just go through a neighborhood of other features.  not all of them. 
                        foreach (SurfaceFeature otherSF in surfaceFeatures)
                        {
                            featuresToMatch.Add(otherSF);
                            // match 2 features? 
                            if (matchUpFeaturesInOrder(featuresToMatch, widget))
                            {
                                // yes. 
                                Point3d secondFeaturePoint =
                                    housingMesh.Vertices[otherSF.indexIntoTheMeshVertexList];
                                //RhinoLog.DrawCylinder(firstFeaturePoint, secondFeaturePoint, 1.0, Color.Aquamarine, doc); 
                                foreach (SurfaceFeature thirdSF in surfaceFeatures)
                                {
                                    featuresToMatch.Add(thirdSF);
                                    // match 3 features? 
                                    if (matchUpFeaturesInOrder(featuresToMatch, widget))
                                    {
                                        // yes we have a match.  rejoice. 
                                        foundAMatchStoreIt(featuresToMatch, widget);
                                        Point3d thirdFeaturePoint =
                                            housingMesh.Vertices[thirdSF.indexIntoTheMeshVertexList];
                                        //RhinoLog.DrawCylinder(secondFeaturePoint, thirdFeaturePoint, 1.0, Color.PaleVioletRed, doc); 
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
        /// we found a matching widget placement, stick it in WidgetPlacements for later use. 
        /// </summary>
        /// <param name="featuresToMatch"></param>
        /// <param name="widget"></param>
        private void foundAMatchStoreIt(List<SurfaceFeature> featuresToMatch, WidgetBlank widget)
        {
            RhinoLog.write("we have a match for " + widget.kind);
            Point3d[] matchPoints = new Point3d[3];
            for (int i = 0; i < 3; i++)
            {
                matchPoints[i] = housingMesh.Vertices[featuresToMatch[i].indexIntoTheMeshVertexList];
            }
            RhinoLog.DrawCylinder(matchPoints[0], matchPoints[1], 0.2, Color.Red, doc);
            RhinoLog.DrawCylinder(matchPoints[1], matchPoints[2], 0.1, Color.Blue, doc);
            RhinoLog.DrawCylinder(matchPoints[0], matchPoints[2], 0.6, Color.Green, doc);
            WidgetPlacements.Add(new WidgetPlacement(matchPoints, widget));
            WidgetPlacements.Last().drawNormal(doc);
            WidgetPlacements.Last().drawCentroid(doc); 
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
                //RhinoLog.debug("matched type"); 
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
            //RhinoLog.debug("for features "+v1+" and " +v2 +" distance in mesh " + distanceBetweenFeatures + " distance in widget " + distanceOnObject); 
            return (distancesCloseEnough(distanceBetweenFeatures, widget.distanceFromFeature(v1, v2))); 
        }

        private bool distancesCloseEnough(double distanceBetweenFeatures, double distanceBetweenFeaturesOnWidget)
        {
            return (Math.Abs(distanceBetweenFeatures - distanceBetweenFeaturesOnWidget) < featureDistanceMatchThreshold); 
        }

        private Point3d getPoint3DForIndex(int index)
        {
            return (housingMesh.Vertices[index]);
        }
        #endregion

        internal void findFeatures(double thresholdEnteredLow, double thresholdEnteredHigh)
        {
            curvatureFeatureThresholdHigh = thresholdEnteredHigh;
            curvatureFeatureThresholdLow = thresholdEnteredLow; 
            findFeatures(); 
        }

        #region related to finding features on the surface. 
        /// <summary>
        /// ignores innie feature points right now .
        /// uses a max and min positive curvature.  
        /// </summary>
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
            for (int i = 0; i < housingMesh.Vertices.Count; i++)
            {
                if (signedMeanCurvatures[i] > curvatureFeatureThresholdLow &&
                    signedMeanCurvatures[i] < curvatureFeatureThresholdHigh)
                {
                    // we have an outie (point poking out with positive curvature) surface feature. 
                    surfaceFeatures.Add(new SurfaceFeature(i,SurfaceFeature.featureType.outie));
                }
            }
            // that's all the features so far.  
            // now rule out features by looking for flat spots.  
            HashSet<SurfaceFeature> flatSpots = findFlatSpots(surfaceFeatures);
            surfaceFeatures.ExceptWith(flatSpots); 

            featuresAreFound = true; 
            RhinoLog.write("found " + surfaceFeatures.Count + " surface features"); 
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
                    if (Math.Abs(dz) < flatThresholdForDz)
                    {
                        flatCount++;
                        vertexColors[neighborIndex] = Color.Gold; 
                    } 
                }
                if (flatCount > minFlatsToBeConsideredNotAFeature)
                {
                    flatSpots.Add(sf); 
                }
            }
            // let's color the flat spots. 
            foreach (SurfaceFeature sf in flatSpots )
            {
                int vertexIndex = sf.indexIntoTheMeshVertexList;
                vertexColors[vertexIndex] = Color.DarkGreen; 
            }
            housingMesh = redrawSurfaceColors(housingMesh, housingMeshGuid, vertexColors);
            return flatSpots; 
        }




        private void calculateSignedMeanCurvature()
        {
            for (int i = 0; i < housingMesh.Vertices.Count; i+= curvatureVertexIncrement)
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
                    double dx = 0 ;
                    double dz = 0;
                    calculateDxAndDz(i,neighborIndex, ref dx, ref dz);
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

        private List<int> getNeighborsBrokenIgnoresTopology (int vertex)
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
            nthGeneration.UnionWith(makeHashSetOf (getNeighborsToplogyAware(startingVertex))); 
            for (int generation = 1; generation <= n; generation ++ )
            {
                everyone.UnionWith(nthGeneration);
                prevGeneration.Clear();
                prevGeneration.UnionWith(nthGeneration); 
                nthGeneration.Clear(); 
                foreach (int previous in prevGeneration)
                {
                    nonUniqueNeighbors = makeHashSetOf (getNeighborsToplogyAware(previous));
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


        internal void colorNthGeneration (int n )
        {
            for (int i = 4; i < housingMesh.Vertices.Count; i += curvatureVertexIncrement)
            {
                HashSet<int> nthGeneration = getOnlyNthGeneration(n, i); 
                foreach (int index in nthGeneration)
                {
                    vertexColors[index] = Color.HotPink; 
                }
                vertexColors[i] = Color.Crimson; 
            }
            housingMesh = redrawSurfaceColors(housingMesh, housingMeshGuid, vertexColors); 
        }



        internal void colorCurvatureByThisRange (double min, double max)
        {
            minCurvatureToColor = min;
            maxCurvatureToColor = max;
            colorCurvature(); 
        }

        internal void colorCurvature()
        {
            if (curvatureIsCalcuated)
            {
                double maxc = -100000;
                double minc = 100000;
                for (int i = 0; i < housingMesh.Vertices.Count; i += curvatureVertexIncrement)
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
                    for (int i = 0; i<housingMesh.Vertices.Count; i+=curvatureVertexIncrement)
                    {
                        double curvature = signedMeanCurvatures[i];
                        if (curvature > minc && curvature < maxc)
                        {
                            // three ways to color  by curvature... 
                            // 1.  
                            // vertexColors[i] = ColorByIncrement(curvature, 0.5);
                            // 2.  
                            vertexColors[i] = colorByRange(curvature, minCurvatureToColor, maxCurvatureToColor); // if curvature is defined as only second gen topological neighbors using 
                                // forward differencing only then 0.7 to 1.5 is a generous range for feature identification on some models. 
                            // 3. 
                            //int colorindex = (int)((curvature - minc) * scaleTo360Range);
                            //  vertexColors[i] = RhinoLog.HsvtoColor(colorindex, 0.6, 0.9);
                        } else
                        {
                            vertexColors[i] = Color.Black;
                            Point3d point = housingMesh.Vertices[i];
                            Vector3d normal = housingMesh.Normals[i];
                            normal.Unitize(); 
                            RhinoLog.DrawCylinder(point, normal, 1.0, 0.2, Color.HotPink, doc); 
                        }
                    }
                    housingMesh = redrawSurfaceColors(housingMesh, housingMeshGuid, vertexColors);
                }
            }
        }

        private Color ColorByIncrement (double value, double increment)
        {
            // let's do three bands. 
            double bandDbl = Math.Abs (value / increment);
            int globalband = (int) Math.Truncate(bandDbl);
            int band = globalband % 4;
            Color[] bandColors = new Color[4] { Color.HotPink, Color.Khaki, Color.Green, Color.Cornsilk};
            return bandColors[band]; 

        }

        private Color colorByRange (double value, double low, double high)
        {
            if (value < high && value > low )
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
            } else
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
                    RhinoLog.DrawCylinder(point, normal, 1.0, 0.2, Color.GreenYellow, doc);
                }
                // make the color changes visible. 
                housingMesh = redrawSurfaceColors(housingMesh,housingMeshGuid, vertexColors); 
            }
        }


        private Mesh rotate(Mesh mesh, double angleDegrees, Vector3d axisOfRotation, Point3d rotationOrigin, Guid guid)
        {
            double angleRadians = angleDegrees * (Math.PI / 180.00);
            mesh.Rotate(angleRadians, axisOfRotation, rotationOrigin);
            return (changeAMesh(mesh, guid)); 
        }

        private Mesh translate(Mesh mesh, Vector3d v, Guid meshGUID)
        {
            mesh.Translate(v);
            return (changeAMesh(mesh, meshGUID)); 
        }

        /// <summary>
        /// replace the mesh object at oldmeshGUID with the new mesh. 
        /// also force a redraw.  
        /// return the new mesh so people can use it if they want.  
        /// </summary>
        /// <param name="newMesh"></param>
        /// <param name="oldMeshGUID"></param>
        /// <returns></returns>
        private Mesh changeAMesh (Mesh newMesh, Guid oldMeshGUID)
        {
            doc.Objects.Replace(oldMeshGUID, newMesh);
            doc.Views.Redraw();
            return newMesh;
        }

        private Mesh redrawSurfaceColors(Mesh meshToColor, Guid meshGUID, Color[] vertexColorsToUse)
        {
            // make a new mesh. 
            Mesh newMesh = meshToColor.DuplicateMesh();
            newMesh.VertexColors.Clear(); 
            for (int i = 0; i < newMesh.Vertices.Count; i++)
            {
                newMesh.VertexColors.SetColor(i, vertexColorsToUse[i]); 
            }
            newMesh.FaceNormals.ComputeFaceNormals();
            return changeAMesh(newMesh, meshGUID); 
        }
        #endregion
    }
}
