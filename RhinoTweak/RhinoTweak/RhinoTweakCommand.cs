using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System.Collections;
using Rhino.DocObjects;
using System.Drawing;

namespace RhinoTweak
{
    [System.Runtime.InteropServices.Guid("fccea0d1-bdca-4c89-9159-71e02ce00e62"),
     Rhino.Commands.CommandStyle(Rhino.Commands.Style.ScriptRunner)
        ]
    public class RhinoTweakCommand : Command
    {
        private List<HousingMesh> housingMeshes;
        List<WidgetBlank> widgetBlanks = new List<WidgetBlank>();
        public RhinoTweakCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        #region rhino boilerplate. 

        ///<summary>The only instance of this command.</summary>
        public static RhinoTweakCommand Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "RhinoTweakCommand"; }
        }
        #endregion

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            List<RhinoObject> stuffIntheDoc = new List<RhinoObject>(); 
            List<MeshObject> meshesInTheDoc = new List<MeshObject>();
            List<HousingMesh> housingMeshes = new List<HousingMesh>();

            double thresholdEnteredHigh = 0.21;
            double thresholdEnteredLow = 0.15;
            // get the curvature threshold. 
            // Rhino.Input.RhinoGet.GetNumber("curvature upper threshold", false, ref thresholdEnteredHigh);
            //Rhino.Input.RhinoGet.GetNumber("curvature upper threshold", false, ref thresholdEnteredLow);
             Rhino.Input.RhinoGet.GetNumber("curvature color lower threshold", false, ref thresholdEnteredLow);
            Rhino.Input.RhinoGet.GetNumber("curvature color upper threshold", false, ref thresholdEnteredHigh);

            makeUpWidgetBlanks(); 

            stuffIntheDoc.AddRange((doc.Objects.FindByObjectType(Rhino.DocObjects.ObjectType.Mesh)));
            if (stuffIntheDoc.Count == 0)
            {
                RhinoLog.write("no meshes in the doc.  We're done");
                return Result.Nothing;
            }
            // a little hokey, but we have to cast these to meshes.  
            foreach (RhinoObject ro in stuffIntheDoc)
            {
                try {
                    meshesInTheDoc.Add((MeshObject)ro);
                } catch (Exception e)
                {
                    RhinoLog.error("findobjectbytype mesh found something other than a mesh.");
                    RhinoLog.error(e.Message); 
                }
            }
            foreach (MeshObject mo in meshesInTheDoc)
            {
                HousingMesh hm = new HousingMesh(mo.MeshGeometry, mo.Id, doc);
                hm.findFeatures();// thresholdEnteredLow,thresholdEnteredHigh);
//                hm.colorNthGeneration(2); 
                hm.colorCurvatureByThisRange(thresholdEnteredLow,thresholdEnteredHigh);
//                hm.colorFeatures();
//                hm.findWidgetSites(widgetBlanks);
//               hm.placeWidgets(); 
                housingMeshes.Add(hm); 
                System.Guid IDofOriginalMesh = mo.Id;
                Mesh theMesh = mo.MeshGeometry;
            }
            doc.Views.Redraw(); 
            return Result.Success;

        }

        private void makeUpWidgetBlanks()
        {
            // the button. 
            WidgetBlank newBlank = new WidgetBlank(WidgetBlank.kinds.magnetic_button);
            // note that the distances from 0 to everything else are the 
            // smallest 2 distances.  
            // note that the distance from 0 to 1 is the smallest distance.  
            newBlank.setDistance(0, 1, 5.0);
            newBlank.setDistance(0, 2, 5.66);
            newBlank.setDistance(1, 2, 7.55);
            newBlank.setAngleInDegrees(0, 1, 2, 90.0);
            newBlank.setAngleInDegrees(1, 2, 0, 48.54);
            newBlank.setAngleInDegrees(2, 1, 0, 41.46);
            // see class for notes about normal is flipped. 
            newBlank.normalisflipped = false; 
            newBlank.setFeatureType(0, SurfaceFeature.featureType.outie);
            newBlank.setFeatureType(1, SurfaceFeature.featureType.outie);
            newBlank.setFeatureType(2, SurfaceFeature.featureType.outie);
            widgetBlanks.Add(newBlank);

            // the button. 
            newBlank = new WidgetBlank(WidgetBlank.kinds.slider);
            // note that the distances from 0 to everything else are the 
            // smallest 2 distances.  
            // note that the distance from 0 to 1 is the smallest distance.  
            newBlank.setDistance(0, 1, 8.0);
            newBlank.setDistance(0, 2, 34.23);
            newBlank.setDistance(1, 2, 34.23);
            newBlank.setAngleInDegrees(0, 1, 2, 83.29);
            newBlank.setAngleInDegrees(1, 2, 0, 83.29);
            newBlank.setAngleInDegrees(2, 1, 0, 13.42);
            // see class for notes about normal is flipped. 
            newBlank.normalisflipped = false;
            newBlank.setFeatureType(0, SurfaceFeature.featureType.outie);
            newBlank.setFeatureType(1, SurfaceFeature.featureType.outie);
            newBlank.setFeatureType(2, SurfaceFeature.featureType.outie);
            widgetBlanks.Add(newBlank);


        }
    }
}
