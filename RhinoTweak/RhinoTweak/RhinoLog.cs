using System;
using Rhino;
using Rhino.Geometry;
using System.Drawing;

namespace RhinoTweak
{
    public static class RhinoLog
    {
        internal static  Boolean writeDebugInfo = true; 
        internal static void write(string v)
        {
            RhinoApp.WriteLine(v); 
        }

        internal static void error(string v)
        {
            write ("ERROR: "+ v);
        }

        internal static void debug(string v)
        {
            if (writeDebugInfo)
            {
                write("DEBUG: " + v);
            }
        }

        internal static void DrawCylinder(Point3d oneEnd, Point3d otherEnd, double radius, RhinoDoc doc)
        {
            DrawCylinder(oneEnd, otherEnd, radius, Color.Purple, doc); 
        }

        internal static void DrawCylinder(Point3d oneEnd, Point3d otherEnd, double radius, Color color, RhinoDoc doc)
        {
            Vector3d axis = oneEnd - otherEnd;
            Vector3d unitAxis = axis;
            unitAxis.Unitize();
            Point3d center = oneEnd - unitAxis * (axis.Length / 1.0);
            Vector3d zaxis = oneEnd - center;
            Plane plane = new Plane(center, zaxis);
            Circle circle = new Circle(plane, radius);
            Cylinder cylinder = new Cylinder(circle, zaxis.Length);
            Brep brep = cylinder.ToBrep(true, true);
            if (brep != null)
            {
                Rhino.DocObjects.ObjectAttributes attributes = new Rhino.DocObjects.ObjectAttributes();
                attributes.ObjectColor = color;
                attributes.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject; 
                doc.Objects.AddBrep(brep, attributes);
                doc.Views.Redraw();
            }
        }


    }
}