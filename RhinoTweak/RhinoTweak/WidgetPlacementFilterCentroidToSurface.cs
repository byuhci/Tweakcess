using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class WidgetPlacementFilterCentroidToSurface : WidgetPlacementFilterInterface
    {

        List<WidgetBlank> blanksToUse;
        private HousingMesh housing;

        public WidgetPlacementFilterCentroidToSurface (List<WidgetBlank> blanksToUse, HousingMesh housing)
        {
            this.blanksToUse = blanksToUse;
            this.housing = housing; 
        }

        public HashSet<WidgetPlacement> filter(HashSet<WidgetPlacement> existingPlacements)
        {
            HashSet<WidgetPlacement> viablePlacements = new HashSet<WidgetPlacement>();
            RhinoLog.debug("centroid to surface placement filter started with " + existingPlacements.Count); 
            viablePlacements.Clear();
            Mesh theMesh = housing.theMesh(); 
            // for each placement. 
            foreach (WidgetPlacement placement in existingPlacements)
            {
                // compute the ray once for this placement.  
                Vector3d normal = placement.normal;
                normal.Unitize();
                Ray3d directionOfNormal = new Ray3d(placement.centroid, normal);
                Ray3d oppositeOfNormal = new Ray3d(placement.centroid, -1 * normal); 
                // see if it's viable for some widget type. 
                foreach (WidgetBlank blank in blanksToUse)
                {
                    // find the surface of the housing in the direction of the normal. 
                    // ASSUME closest point is the one we want.  
                    // have to go in each direction and take the one with the smallest
                    // magnitude. 
                    double distanceAlongNormal =
                        Rhino.Geometry.Intersect.Intersection.MeshRay(
                            theMesh, directionOfNormal); 
                    double distanceOppositeNormal =
                        Rhino.Geometry.Intersect.Intersection.MeshRay(
                            theMesh, oppositeOfNormal);
                    double offsetFromSurface = 2000000;
                    #region offsetFromSurface = find closest signed distance to intersection. 
                    // this gets a little tricky becaus I'm using a negative 
                    // distance along the normal to mean "the surface is behind you" 
                    // and rhino is using a netgative return value to mean 
                    // "there's no intersection in that direction from here." 
                    // so first we see if either of the values are < 0, which means
                    // no surface in that direciton.  If so, set it to a large value. 
                    if (distanceAlongNormal < 0)
                    {
                        distanceAlongNormal = double.MaxValue;
                    }
                    if (distanceOppositeNormal < 0)
                    {
                        distanceOppositeNormal = double.MaxValue; 
                    }
                    // ok, now the smallest one is the closest one,  But I 
                    // also have to get the sign right.  
                    // closest intersection is along normal.  Great. 
                    if (distanceAlongNormal < distanceOppositeNormal)
                    {
                        offsetFromSurface = distanceAlongNormal; 
                    }
                    // closest interaction is in direction opposite the normal.
                    // flip the sign. 
                    if (distanceAlongNormal > distanceOppositeNormal)
                    {
                        offsetFromSurface = -distanceOppositeNormal; 
                    }
                    // if that all worked then offsetfromsurface contains the 
                    // signed distance along the normal from the cnetroid to the 
                    // closet point on the surface. 
                    #endregion
                    double errorInOffset =
                        Math.Abs(offsetFromSurface - blank.getCentroidOffsetFromSurfaceAlongNormal()); 
                    if (errorInOffset < Constants.maxAllowableErrorInSurfaceOffsetInWidgetPlacement)
                    {
                        viablePlacements.Add(placement); 
                    }
                }
            }
            RhinoLog.debug("centroid to surface placement filter return " + viablePlacements.Count);
            return viablePlacements; 

        }
    }
}
