using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class WidgetBlank
    {
        // some conventions. 
        // 1.  you pick feature 0 however you like. 
        // 2.  but feature 1 is the one closest to 0.  
        // 3.  and feature 2 is the one farthest from 0. 
        // 4.  you can't have 1 and 2 the same distance from 0.  

        private SurfaceFeature.featureType[] featureTypes;
        private double[,] distances;
        private double[,,] angles;
         public string name {
            get; }

        public WidgetBlank (SurfaceFeature.featureType[] types, 
                            double [,] distances, 
                            double [,,] angles,
                            string widgetName)
        {
            featureTypes = types;
            this.distances = distances;
            this.angles = angles;
            this.name = widgetName; 
            checkDistancesArray();
            checkAnglesArray();  
        }

        public WidgetBlank(string name)
        {
            this.name = name;
            angles = new double[3, 3, 3];
            distances = new double[3, 3];
            featureTypes = new SurfaceFeature.featureType[3];
        }

        public void importSTLFile ()
        {
            string fileName = Constants.workingDirectory+name.Replace(" ","-")+".stl"; 
            if (!System.IO.File.Exists(fileName))
            {
                RhinoLog.error("couldn't find file " + fileName);
                return; 
            } else
            {
                RhinoLog.debug("found file " + fileName);
                string script = "_-Import \"" + fileName + "\" _Enter";
                RhinoLog.debug("command: " + script); 
                Boolean scriptResult = Rhino.RhinoApp.RunScript(script, true);
                RhinoLog.debug("result: " + scriptResult.ToString()); 
            }
        } 
          
        private void checkAnglesArray()
        {
            if (angles.GetLength(0) != 3 && angles.GetLength(1) != 3 && 
                angles.GetLength(2) != 3)
            {
                RhinoLog.error("array array for " + name + " has wrong dimensions");
            }
            // TODO check symmertry. 
        }

        private void checkDistancesArray()
        {
            if (distances.GetLength(0) != 3 && distances.GetLength(1) != 3)
            {
                RhinoLog.error("distances array for " + name + " has wrong dimensions");
            }
            // make sure it's symmetric. 
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (distances[i, j] != distances[j, i])
                    {
                        RhinoLog.error("distances not symetric in " + name + " for " + i + "," + j);
                    }
                }
            }
            if (distances[0,1] >= distances[0,2])
            {
                RhinoLog.error("you failed to satisfy the conventions for " + name + " bow your head in abject misery"); 
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
