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
        private List<SurfaceFeature> surfaceFeatures;
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
        private double curvatureFeatureThreshold = 0.21;
        private double featureDistanceMatchThreshold = 0.25;
        private double featureAngleMatchThreshold = 3.2;

        public HousingMesh (Mesh m, Guid m2manage, RhinoDoc doc)
        {
            WidgetPlacements = new List<WidgetPlacement>();
            surfaceFeatures = new List<SurfaceFeature>();
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
            RhinoLog.debug("importing widget " + placement.widget.name);
            placement.widget.importSTLFile(whichPiece);
            // the guid is the guid of the last mesh in the list. 
            List<RhinoObject> stuffInTheDoc = new List<RhinoObject>();
            stuffInTheDoc.AddRange(doc.Objects.GetSelectedObjects(false, false));
            if (stuffInTheDoc.Count != 1)
            {
                RhinoLog.error("should have only imported one mesh for the widget");
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
            Vector3d axisOfRotation = Vector3d.CrossProduct(Vector3d.ZAxis, placement.normal);
            RhinoLog.DrawCylinder(Point3d.Origin, axisOfRotation, 1.0, 0.1, Color.BurlyWood, doc);
            double rotationAngleDegreees =
                myAngleFinder(Vector3d.ZAxis, placement.normal);
            widgetMesh = rotate(widgetMesh, rotationAngleDegreees, axisOfRotation, Point3d.Origin, widgetMeshGUID);

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
                                        foundAMatchStoreIt(featuresToMatch, widget); 
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
            RhinoLog.write("we have a match for " + widget.name);
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

        #region related to finding features on the surface. 
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
            for (int i = 0; i < housingMesh.Vertices.Count; i++)
            {
                Point3d theVertex = housingMesh.Vertices[i];
                Vector3d vertexNormal = housingMesh.Normals[i];
                vertexNormal.Unitize();
                // draw the vertex normal. 
                //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 10.0));

                // get the neighbors. 
                ArrayList neighborIndices = new ArrayList();
                neighborIndices.AddRange(housingMesh.Vertices.GetConnectedVertices(i));
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
                    Point3d neighborVertex = housingMesh.Vertices[neighborIndex];
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
