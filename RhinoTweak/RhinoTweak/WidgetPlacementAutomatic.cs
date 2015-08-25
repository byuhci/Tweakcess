using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class WidgetPlacementAutomatic : AbstractWidgetLocationFinder
    {
 
        public WidgetPlacementAutomatic (Mesh housingMesh, HashSet<SurfaceFeature> featurePoints, List<WidgetBlank> widgetBlanks, Rhino.RhinoDoc doc)
        :base(housingMesh,featurePoints,widgetBlanks,doc)
        {
        }

        /// <summary>
        ///  go through this list of widget blanks and find all the widget placements
        /// that match those blanks by analyzing the surface features found on this 
        /// mesh.  
        /// Stores placements in the widgetplacements member.  
        /// </summary>
        /// <param name="widgetBlanks"></param>
        public override HashSet<WidgetPlacement> findPlacements()
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
                                    }
                                    featuresToMatch.Remove(thirdSF);
                                }
                            }
                            featuresToMatch.Remove(otherSF);
                        }
                    }
                }
            }
            return WidgetPlacements; 
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
            WidgetPlacements.Add(new WidgetPlacement(matchPoints, widget));
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
                couldBeAmatch = widget.nthFeatureIsType(0, surfaceFeatures[0]);
                //RhinoLog.debug("matched type"); 
            }
            if (surfaceFeatures.Count >= 2 && couldBeAmatch)
            {
                // check type of feature 1. 
                couldBeAmatch = couldBeAmatch && widget.nthFeatureIsType(1, surfaceFeatures[1]);
                // check distances. 
                couldBeAmatch = couldBeAmatch && checkDistances(0, 1, surfaceFeatures, widget);
            }
            if (surfaceFeatures.Count >= 3 && couldBeAmatch)
            {
                // check type of feature 2 
                couldBeAmatch = couldBeAmatch && widget.nthFeatureIsType(2, surfaceFeatures[2]);
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

        private Point3d getPoint3DForIndex(int indexIntoTheMeshVertexList)
        {
            return (Utility.getPoint3dforIndex(indexIntoTheMeshVertexList, housingMesh)); 
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
            Vector3d v1tov2 = p2 - p1;
            Vector3d v1tov3 = p3 - p1;
            // angle is in degrees. 
            double angleOnHousing = Utility.myAngleFinder(v1tov2, v1tov3);
            double angleOnWidget = widget.angleBetween(surfaceFeatureIndex1, sfi2, sfi3);
            return (anglesAreCloseEnough(angleOnHousing, widget.angleBetween(surfaceFeatureIndex1, sfi2, sfi3)));
        }


        private bool anglesAreCloseEnough(double angleOnHousing, double p)
        {
            return (Math.Abs(angleOnHousing - p) < Constants.featureAngleMatchThreshold);
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
            return (Math.Abs(distanceBetweenFeatures - distanceBetweenFeaturesOnWidget) < Constants.featureDistanceMatchThreshold);
        }


    }
}
