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
        private double tooCloseThreshold = 0.8;
        private double minValueForNormalsToBeTooClose = 0.9;

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

        internal bool sameKindAs(WidgetPlacement otherPlacement)
        {
            WidgetBlank.kinds myType = widget.kind;
            WidgetBlank.kinds otherType = otherPlacement.widget.kind;
            return (myType.Equals(otherType)); 
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

        internal void drawLinksBetweenFeatures(RhinoDoc doc)
        {
            // thick one from 0 to 1. 
            RhinoLog.DrawCylinder(matchPoints[0], matchPoints[1], 0.7, Color.Red, doc);
            // medium from 0 to 2 
            RhinoLog.DrawCylinder(matchPoints[0], matchPoints[2], 0.3, Color.Blue, doc);
            // skinny from 2 to 3
            RhinoLog.DrawCylinder(matchPoints[1], matchPoints[2], 0.1, Color.Green, doc);
        }

        internal bool isTooCloseButNotEqualTo(WidgetPlacement placement2)
        {
            Point3d centroid2 = placement2.centroid;
            double distance = centroid.DistanceTo(centroid2);
            return (distance < tooCloseThreshold && distance != 0);


        }

        internal bool pointSameWayAs(WidgetPlacement placement2)
        {
            Vector3d normal2 = placement2.normal;
            normal.Unitize();
            normal2.Unitize();
            double dotProduct = (normal * normal2) / (normal2.Length * normal.Length) ;
            return (dotProduct > minValueForNormalsToBeTooClose) ; 
        }

        internal void mergeWith(HashSet<WidgetPlacement> otherPlacements)
        {
            foreach (WidgetPlacement otherPlacement in otherPlacements)
            {
                // add together the points. 
                for (int i = 0; i < 3; i++)
                {
                    matchPoints[i] = (matchPoints[i] + otherPlacement.matchPoints[i]);
                }
            }
            // divide by number of other placements plus 1. 
            for (int i = 0; i < 3; i++)
            {
                matchPoints[i] = matchPoints[i] / (1+otherPlacements.Count); 
            }
            // caculate a new centroid. 
            calculateCentroid();
            // calculate a new normal.  
            calculateNormal(); 
        }
    }
}
