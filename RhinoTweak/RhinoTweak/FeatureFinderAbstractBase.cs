using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace RhinoTweak
{
    public abstract class FeatureFinderAbstractBase 
    {

        internal Mesh housingMesh;
        internal Rhino.RhinoDoc doc;
        /// <summary>
        ///  put your features here. 
        /// </summary>
        internal HashSet<SurfaceFeature> surfaceFeatures;
        /// <summary>
        ///  if you want to color the surface, and you will, put your 
        /// colors here.  Indexed by vertex index. 
        /// </summary>
        internal Color[] vertexColors;


        public FeatureFinderAbstractBase(Mesh housingMesh, Rhino.RhinoDoc doc)
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

        /// <summary>
        ///  we tend to need this for curvature based feature finders and we have a couple of thos
        /// so put it here. 
        /// </summary>
        /// <param name="vertexIndex"></param>
        /// <param name="neighborVertexIndex"></param>
        /// <param name="dx"></param>
        /// <param name="dz"></param>
        internal void calculateDxAndDz(int vertexIndex, int neighborVertexIndex, ref double dx, ref double dz)
        {
            Point3d theVertex = housingMesh.Vertices[vertexIndex];
            Point3d neighborVertex = housingMesh.Vertices[neighborVertexIndex];
            Vector3d vertexNormal = housingMesh.Normals[vertexIndex];
            vertexNormal.Unitize();
            // compute the rate of change from here to neighbor as a forward difference. 
            // using dx/dz in theVertex's local coordinate system.  
            Vector3d vertexToNeighbor = neighborVertex - theVertex;
            Vector3d localY = Vector3d.CrossProduct(vertexToNeighbor, vertexNormal);
            localY.Unitize();
            Vector3d localX = Vector3d.CrossProduct(vertexNormal, localY);
            //doc.Objects.AddLine(new Line(theVertex, localX, 5.0));
            //doc.Objects.AddLine(new Line(theVertex, localY, 5.0));
            //doc.Objects.AddLine(new Line(theVertex, vertexNormal, 5.0));
            dx = vertexToNeighbor * localX;
            dz = vertexToNeighbor * vertexNormal;
        }

        internal abstract void findFeatures();

        internal HashSet<SurfaceFeature> getFeatures()
        {
            return surfaceFeatures; 
        }

        internal abstract Color[] colorize();


        internal HashSet<int> getOnlyNthGeneration(int n, int startingVertex)
        {
            HashSet<int> nthGeneration = new HashSet<int>();
            HashSet<int> everyone = new HashSet<int>();
            HashSet<int> prevGeneration = new HashSet<int>();
            HashSet<int> nonUniqueNeighbors = new HashSet<int>();
            nthGeneration.UnionWith(makeHashSetOf(getNeighborsToplogyAware(startingVertex)));
            for (int generation = 1; generation <= n; generation++)
            {
                everyone.UnionWith(nthGeneration);
                prevGeneration.Clear();
                prevGeneration.UnionWith(nthGeneration);
                nthGeneration.Clear();
                foreach (int previous in prevGeneration)
                {
                    nonUniqueNeighbors = makeHashSetOf(getNeighborsToplogyAware(previous));
                    nthGeneration.UnionWith(nonUniqueNeighbors);
                }
                nthGeneration.ExceptWith(everyone);
            }
            return nthGeneration;
        }

        private HashSet<int> makeHashSetOf(List<int> list)
        {
            HashSet<int> returnValue = new HashSet<int>();
            foreach (int i in list)
            {
                returnValue.Add(i);
            }
            return returnValue;
        }

        private void addListToHashSet(List<int> list, ref HashSet<int> nthGeneration)
        {
            foreach (int member in list)
            {
                nthGeneration.Add(member);
            }
        }

        private List<int> getNeighborsToplogyAware(int vertex)
        {
            List<int> neighbors = new List<int>();
            Rhino.Geometry.Collections.MeshTopologyVertexList mtvl = housingMesh.TopologyVertices;
            int vertexTopo = mtvl.TopologyVertexIndex(vertex);
            int[] neighborsTopo = mtvl.ConnectedTopologyVertices(vertexTopo);
            for (int neighborTopoIndex = 0; neighborTopoIndex < neighborsTopo.Length; neighborTopoIndex++)
            {
                int[] neighborVertices = mtvl.MeshVertexIndices(neighborsTopo[neighborTopoIndex]);
                for (int neighborIndex = 0; neighborIndex < neighborVertices.Length; neighborIndex++)
                {
                    neighbors.Add(neighborVertices[neighborIndex]);
                }
            }
            return neighbors;
        }

    }
}