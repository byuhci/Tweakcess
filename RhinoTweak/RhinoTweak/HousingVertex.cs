using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    /// <summary>
    /// keeps track of attributes of a vertex.  
    /// doesn't store the rest of the mesh ever.  
    /// primarily a helper for HousingMesh. 
    /// </summary>
    class HousingVertex
    {
        public Color color
        {
            get; set;
        }        
        public double signedMeanCurvature 
        {
            get; set;
        }
        
        public HousingVertex ()
        {
            // not much to do . 
        }

    }
}
