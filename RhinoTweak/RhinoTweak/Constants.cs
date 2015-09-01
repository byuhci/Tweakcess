using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    static class Constants
    {
        public static string workingDirectory = (Environment.CurrentDirectory) + "\\..\\..\\";

        public static double featureDistanceMatchThreshold = 0.55;
        public static double featureAngleMatchThreshold = 3.2;
        public static int curvatureVertexIncrement = 1;
        public static double minCurvatureToColor = 0.9;
        public static double maxCurvatureToColor = 1.5;
        public static double flatThresholdForDz = 0.09;
        public static int minFlatsToBeConsideredNotAFeature = 2;
        public static double centroidDifferenceThresholdToMerge = 0.09;
        public static double curvatureFeatureThresholdLow = 0.6;  // dropped to 0.6 on 8/28
        public static double curvatureFeatureThresholdHigh = 1.5;

    }
}
