using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    class WidgetPlacementRemoveDuplicates : WidgetPlacementFilterInterface
    {
        public WidgetPlacementRemoveDuplicates()
        {
            // not much to do. 
        }

        public HashSet<WidgetPlacement> filter(HashSet<WidgetPlacement> existingPlacements)
        {
            RhinoLog.debug("starting with " + existingPlacements.Count + " placements"); 
           HashSet<WidgetPlacement> matchingPlacements = new HashSet<WidgetPlacement>();
            HashSet<WidgetPlacement> filteredPlacements = new HashSet<WidgetPlacement>(); 
            foreach (WidgetPlacement placement in existingPlacements)
            {
                matchingPlacements.Clear();
                foreach (WidgetPlacement otherPlacement in existingPlacements)
                {
                    Boolean tooClose = placement.isTooCloseButNotEqualTo(otherPlacement);
                    Boolean pointsSameWay = placement.pointSameWayAs(otherPlacement);
                    Boolean sameKind = placement.sameKindAs(otherPlacement); 
                    //RhinoLog.debug(">> tooclose? " + tooClose + " points same way? " + pointsSameWay); 
                    if (tooClose && pointsSameWay)
                    {
                        if (!sameKind)
                        {
                        } else
                        {
                            matchingPlacements.Add(otherPlacement);
                        }
                    }                    
                }
                placement.mergeWith(matchingPlacements);
                filteredPlacements.Add(placement);
            }
            RhinoLog.debug("returning " + filteredPlacements.Count); 
            return filteredPlacements; 
        }
    }
}
