using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace RhinoTweak
{
    public abstract class AbstractFeatureFinder 
    {

        internal Mesh housingMesh;
        internal Rhino.RhinoDoc doc;
        internal HashSet<SurfaceFeature> surfaceFeatures;
        internal Color[] vertexColors;


        public AbstractFeatureFinder(Mesh housingMesh, Rhino.RhinoDoc doc)
        {
            this.housingMesh = housingMesh;
            this.doc = doc;
            surfaceFeatures = new HashSet<SurfaceFeature>();
            vertexColors = new Color[housingMesh.Vertices.Count]; 
            for (int i = 0; i < housingMesh.Vertices.Count; i++)
            {
                vertexColors[i] = Color.Bisque; 
            }
        }

        internal abstract void findFeatures();

        internal HashSet<SurfaceFeature> getFeatures()
        {
            return surfaceFeatures; 
        }

        internal abstract Color[] colorize();
    }
}