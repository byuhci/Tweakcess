using System;
using Rhino;
using Rhino.Geometry;
using System.Drawing;

namespace RhinoTweak
{
    public static class RhinoLog
    {
        internal static  Boolean writeDebugInfo = true;
        internal static Boolean drawGeometry = true; 
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
            dealWithTheBrep (cylinder.ToBrep(true, true),color, doc);
           }

        internal static void DrawSphere(Point3d center, double radius, Color color, RhinoDoc doc)
        {
            Sphere sphere = new Sphere(center, radius);
            dealWithTheBrep(sphere.ToBrep(), color, doc); 
        }

        internal static void DrawCylinder(Point3d oneEnd, Vector3d axis, double length, double radius, Color color, RhinoDoc doc)
        {
            DrawCylinder(oneEnd, oneEnd + (axis * length), radius, color, doc); 
        }

        private static void dealWithTheBrep(Brep brep, Color color, RhinoDoc doc)
        {
            if (brep != null && drawGeometry)
            {
                Rhino.DocObjects.ObjectAttributes attributes = new Rhino.DocObjects.ObjectAttributes();
                attributes.ObjectColor = color;
                attributes.ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject;
                doc.Objects.AddBrep(brep, attributes);
                doc.Views.Redraw();
            }
        }

        public static Color HsvtoColor (double h, double s, double v)
        {
            int r, g, b;
            HsvToRgb(h, s, v, out r, out g, out b);
            return (Color.FromArgb(r, g, b)); 
        }

            /// <summary>
            /// Convert HSV to RGB
            /// h is from 0-360
            /// s,v values are 0-1
            /// r,g,b values are 0-255
            /// Based upon http://ilab.usc.edu/wiki/index.php/HSV_And_H2SV_Color_Space#HSV_Transformation_C_.2F_C.2B.2B_Code_2
            /// </summary>
        public static void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        private static int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }


    }
}