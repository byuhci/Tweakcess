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
        // we expect the centroid to be a certain distance from the 
        // surface along the normal.  Are we close enough? 
        // 0.75 or more gives two placements.  0.6  or less gives none. 
        // 0.7 gives the wrong one. 
        internal static double maxAllowableErrorInSurfaceOffsetInWidgetPlacement = 1.0;
        internal static double maxAllowableErrorInNormalsDotProduct = 0.2;
        internal static double maxThresholdForCurvatureInFeatureIdentification = 1.5;
        internal static double minThresholdForCurvatureInFeatureIdentification = 0.15;
    }
}
