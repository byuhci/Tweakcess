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
            theMesh.FaceNormals.ComputeFaceNormals(); 
            // for each placement. 
            foreach (WidgetPlacement placement in existingPlacements)
            {
                // compute the ray once for this placement.  
                Vector3d normal = placement.normal;
                normal.Unitize();
                Boolean surfaceIsClosestInFront = false; 
                Ray3d directionOfNormal = new Ray3d(placement.centroid, normal);
                Ray3d oppositeOfNormal = new Ray3d(placement.centroid, -1 * normal);
                // see if it's viable for some widget type. 
                // -- no need to go through all the blank types, just get the 
                // one that this is.  
                WidgetBlank blank = placement.widget; 
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
                       surfaceIsClosestInFront = true; 
                    }
                    // closest interaction is in direction opposite the normal.
                    // flip the sign. 
                    if (distanceAlongNormal > distanceOppositeNormal)
                    {
                        offsetFromSurface = -distanceOppositeNormal;
                        surfaceIsClosestInFront = false; 
                    }
                // if that all worked then offsetfromsurface contains the 
                // signed distance along the normal from the cnetroid to the 
                // closet point on the surface. 
                #endregion
                // is the centroid of the placement inside the mesh?  
                Boolean placementCentroidIsInMesh =
                    (distanceAlongNormal != Double.MaxValue && distanceOppositeNormal != Double.MaxValue); 
                    double errorInOffset =
                        Math.Abs(offsetFromSurface - blank.getCentroidOffsetFromSurfaceAlongNormal());
                // if the dot product of the placement and face normal is worth using, then use it. 
                Boolean dotProductTestFails = false; 
                if (blank.isDotProductOfFaceNormalWorthUsing)
                {
                    int[] closestFaces = new int[10];
                    if (surfaceIsClosestInFront)
                    {
                        // go along the normal. 
                        Rhino.Geometry.Intersect.Intersection.MeshRay(theMesh, directionOfNormal, out closestFaces);
                    }
                    else
                    {
                        // go the other direction. 
                        Rhino.Geometry.Intersect.Intersection.MeshRay(theMesh, oppositeOfNormal, out closestFaces);
                    }
                    // just take the first face normal. 
                    Vector3d faceNormal = theMesh.FaceNormals[closestFaces[0]];
                    faceNormal.Unitize();
                    double normalsDotProduct = placement.normal * faceNormal / (placement.normal.Length * faceNormal.Length);
                    double errorInNormalsDotProduct = Math.Abs(normalsDotProduct - blank.dotProductOfNormalAndMeshFaceNormal);
                    RhinoLog.debug("normals dot product " + normalsDotProduct + " error in normal " + errorInNormalsDotProduct); 
                    if (errorInNormalsDotProduct < Constants.maxAllowableErrorInNormalsDotProduct)
                    {
                        dotProductTestFails = false; 
                    } else
                    {
                        dotProductTestFails = true; 
                    }

                }
                // are we close enough to the right offset and are we in the mesh if should be?  
                // (or out of the mesh if we should be?) 
                if (errorInOffset < Constants.maxAllowableErrorInSurfaceOffsetInWidgetPlacement &&
                        placementCentroidIsInMesh == blank.centroidIsInsideBlank && 
                        ! dotProductTestFails)
                    {
                        viablePlacements.Add(placement);
                    RhinoLog.debug("min distance from placement to surface is " + offsetFromSurface + " from the surface");
                    RhinoLog.debug(" positive direction " + distanceAlongNormal + " negative direction " + distanceOppositeNormal); 
                    RhinoLog.debug(" and I was looing for " + blank.getCentroidOffsetFromSurfaceAlongNormal()); 
                    }
            }
            RhinoLog.debug("centroid to surface placement filter return " + viablePlacements.Count);
            return viablePlacements; 

        }
    }
}
