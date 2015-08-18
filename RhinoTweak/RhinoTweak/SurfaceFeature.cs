using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class SurfaceFeature
    {

        public enum featureType {outie, innie }; 

        public int indexIntoTheMeshVertexList {
            get; set; }
        public Color color { get; set; }
        public featureType typeOfFeature { get; set; }


        public SurfaceFeature(int i)
        {
            this.indexIntoTheMeshVertexList = i;
            color = Color.Fuchsia; 
        }

        public SurfaceFeature(int i, Color red) : this(i)
        {
            this.color = red;
        }
        
        public SurfaceFeature (int i , featureType f): this(i)
        {
            this.typeOfFeature = f;
        }
    }
}
