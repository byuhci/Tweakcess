using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class WidgetBlank
    {
        // some conventions. 
        // 1.  feature 0 has features 1 and 2 closest.  it has the 
        //     minimal distance to any other feature.  
        // 2.  but feature 1 is the one closest to 0.  
        // 3.  and feature 2 is the one farthest from 0. 
        // 4.  you can't have 1 and 2 the same distance from 0.  

        // there's different kinds of elements and each has a role
        // we'll use the pieces enum to generate the file name of the 
        // stl file that contains the piece we are looking for as 
        // <name of widget><pieces.toString()>.stl
        public enum pieces { slug, blank, bracket}; 
        private SurfaceFeature.featureType[] featureTypes;
        private double[,] distances;
        private double[,,] angles;
         public string name {
            get; }

        // the normal is calculated as vector from point 0 to point 1 
        // crossed with vector from point 0 to point 2.  
        // if you want that flipped because your points are laid out
        // differently then set this to true.  
        public Boolean normalisflipped { get; set; }

        public WidgetBlank(string name)
        {
            this.name = name;
            angles = new double[3, 3, 3];
            distances = new double[3, 3];
            featureTypes = new SurfaceFeature.featureType[3];
            normalisflipped = false; 
        }

        public void importSTLFile (pieces whichOne)
        {
            string fileName = name.Replace(" ", "-") + "-"+ whichOne.ToString()+ ".stl"; 
            string fullyQualifiedFileName = Constants.workingDirectory+fileName; 
            if (!System.IO.File.Exists(fullyQualifiedFileName))
            {
                RhinoLog.error("couldn't find file " + fullyQualifiedFileName);
                return; 
            } else
            {
                RhinoLog.debug("found file " + fullyQualifiedFileName);
                string script = "_-Import \"" + fullyQualifiedFileName + "\" _Enter";
                RhinoLog.debug("command: " + script); 
                Boolean scriptResult = Rhino.RhinoApp.RunScript(script, true);
                RhinoLog.debug("result: " + scriptResult.ToString()); 
            }
        } 
          

        /// <summary>
        /// what's the distance between these two feature? 
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        internal double distanceFromFeature(int v1, int v2)
        {
            return (distances[v1, v2]); 
        }

        internal double angleBetween(int v1, int v2, int v3)
        {
            return (angles[v1,v2,v3]);
        }

        internal void setDistance(int v1, int v2, double v3)
        {
            if (v1 > 2 || v2 > 2)
            {
                RhinoLog.error("invalid distance index"); 
            }
            distances[v1, v2] = v3;
            distances[v2, v1] = v3; 
        }

        internal void setAngleInDegrees(int v1, int v2, int v3, double v4)
        {
            if (v1 > 2 || v2 > 2 || v3 > 2)
            {
                RhinoLog.error("invalid angle index"); 
            }
            angles[v1, v2, v3] = v4;
            angles[v1, v3, v2] = v4; 
        }

        internal void setFeatureType(int v, SurfaceFeature.featureType daKin)
        {
            if (v > 2 )
            {
                RhinoLog.error("feature type index too big. ");
            }
            featureTypes[v] = daKin; 
        }

        /// <summary>
        ///  is the nth feature in this widget the same type as 
        /// the type of this feature? 
        /// </summary>
        /// <param name="n"></param>
        /// <param name="surfaceFeature"></param>
        /// <returns></returns>
        internal bool nthFeatureIsType(int n, SurfaceFeature surfaceFeature)
        {
            return (featureTypes[n] == surfaceFeature.typeOfFeature); 
        }
    }
}
