using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    abstract class AbstractWidgetLocationFinder
    {
        protected Mesh housingMesh;
        protected Rhino.RhinoDoc doc;
        protected HashSet<SurfaceFeature> surfaceFeatures;
        protected List<WidgetBlank> widgetBlanks;
        protected HashSet<WidgetPlacement> WidgetPlacements;


        public AbstractWidgetLocationFinder(Mesh housingMesh, HashSet<SurfaceFeature> featurePoints, List<WidgetBlank> widgetBlanks, Rhino.RhinoDoc doc)
        {
            this.housingMesh = housingMesh;
            this.doc = doc;
            this.widgetBlanks = widgetBlanks;
            this.surfaceFeatures = featurePoints;
            WidgetPlacements = new HashSet<WidgetPlacement>();
        }

        public abstract HashSet<WidgetPlacement> findPlacements();

        public HashSet<WidgetPlacement> getPlacements ()
        {
            return WidgetPlacements; 
        } 
        public void showWidgetSites()
        {
            foreach (WidgetPlacement placement in WidgetPlacements)
            {
                placement.drawCentroid(doc);
                placement.drawNormal(doc);
//                placement.drawLinksBetweenFeatures(doc);

            }
        }

        /// <summary>
        /// give me a list of filters and I'll run the widge placements 
        /// list through those filters.  Updating my internal state as I go. 
        /// </summary>
        /// <param name="widgetFilters"></param>
        internal void filterPlacements(List<WidgetPlacementFilterInterface> widgetFilters)
        {
            foreach (WidgetPlacementFilterInterface filter in widgetFilters)
            {
                WidgetPlacements = filter.filter(WidgetPlacements); 
            }
        }
    }
}
