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
        private List<HousingVertex> housingVertices;
        private Mesh housingMesh;
        private System.Guid housingMeshGuid;
        private RhinoDoc doc;
        private AbstractWidgetLocationFinder widgetPlacer; 
        /// <summary>
        ///  indexed by vertex incides. 
        /// </summary>
        private Color[] vertexColors; 
        Boolean curvatureIsCalcuated = false;
        Boolean meshGeometryHasChanged = false;
        Boolean featuresAreFound = false; 


        public HousingMesh (Mesh m, Guid m2manage, RhinoDoc doc)
        {
            housingVertices = new List<HousingVertex>();
            this.doc = doc;

            // deep copy or this? mdj 
            housingMesh = m.DuplicateMesh();
            housingMeshGuid = m2manage;

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

        public void placeWidgets (HashSet<WidgetPlacement> widgetplacements)
        {
            foreach (WidgetPlacement placement in widgetplacements)
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

        internal Mesh theMesh()
        {
            return housingMesh; 
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
                Utility.myAngleFinder(Vector3d.ZAxis, placement.normal);
            widgetMesh = rotate(widgetMesh, rotationAngleDegreees, axisOfRotationInZ, Point3d.Origin, widgetMeshGUID);

            // ASSUME that the widget x axis is aligned with the world x axis on import. 
            Vector3d rotatedWidgetXAxis = Vector3d.XAxis;
            double rotationAngleRadians = rotationAngleDegreees * (Math.PI / 180.0); 
            // and we just rotated the widget so we have to rotate it's local x axis.  
            rotatedWidgetXAxis.Rotate(rotationAngleRadians, axisOfRotationInZ); 
            Vector3d axisOfRotationInX = Vector3d.CrossProduct(rotatedWidgetXAxis, placement.xaxis);
            double rotationAngleDegreesX =
                Utility.myAngleFinder(rotatedWidgetXAxis, placement.xaxis);
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


        private Point3d getPoint3DForIndex(int index)
        {
            return (Utility.getPoint3dforIndex(index, housingMesh)); 
        }
  

        #region related to finding features on the surface. 


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

        private Mesh redrawSurfaceColors(Mesh aMesh, Guid meshGUID, Color[] listofcolors)
        {
            // make a new mesh. 
            Mesh newMesh = aMesh.DuplicateMesh();
            newMesh.VertexColors.Clear();
            for (int i = 0; i < newMesh.Vertices.Count; i++)
            {
                newMesh.VertexColors.SetColor(i, listofcolors[i]);
            }
            newMesh.FaceNormals.ComputeFaceNormals();
            return changeAMesh(newMesh, meshGUID);
        }


        public Mesh redrawHousingMesh(Color[] vertexColorsToUse)
        {
            return redrawSurfaceColors(housingMesh, housingMeshGuid, vertexColorsToUse); 
        }
        #endregion
    }
}
