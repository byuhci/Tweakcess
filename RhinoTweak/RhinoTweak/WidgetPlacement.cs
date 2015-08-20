using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino.Geometry;
using Rhino;
using System.Drawing;

namespace RhinoTweak
{
    class WidgetPlacement
    {
        private Point3d[] matchPoints;
        internal Vector3d xaxis
        {
            get; set;
        }
        public Vector3d normal
        {
            get; set;
        }
        public Point3d centroid { get; set; } 
        public WidgetBlank widget { get; set; }
        
        public WidgetPlacement(Point3d[] matchPoints, WidgetBlank widget1)
        {
            this.matchPoints = matchPoints;
            this.widget = widget1;
            calculateCentroid();
            calculateNormal();
            calculateXAxis(); 
         }

        /// <summary>
        ///  see WidgetBlank class for notes on when and how to flip the normal. 
        /// </summary>
        private void calculateNormal()
        {
            Vector3d v0to1 = matchPoints[1] - matchPoints[0];
            Vector3d v0to2 = matchPoints[2] - matchPoints[0];

            normal = Vector3d.CrossProduct(v0to1, v0to2);
            normal.Unitize(); 
            if (widget.normalisflipped)
            {
                normal = -1 * normal; 
            }
        }

        /// <summary>
        /// by convention by xaxis is the vector that connects 
        /// point 0 with point 1.  
        /// </summary>
        private void calculateXAxis ()
        {
            xaxis = matchPoints[1] - matchPoints[0];
            xaxis.Unitize(); 
        }

        private void calculateCentroid()
        {
            Point3d sumofthree = new Point3d(); 
            for (int i = 0; i < 3; i++)
            {
                sumofthree += matchPoints[i]; 
            }
            centroid = sumofthree / 3.0; 
        }

        internal void drawNormal(RhinoDoc doc)
        {
            RhinoLog.DrawCylinder(centroid, normal, 0.3, 0.25, Color.DeepSkyBlue,doc);
        }

        internal void drawCentroid(RhinoDoc doc)
        {
            RhinoLog.DrawSphere(centroid, 0.5, Color.HotPink,doc);
        }
    }
}
