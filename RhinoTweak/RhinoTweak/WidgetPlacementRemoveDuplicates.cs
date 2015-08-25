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
            HashSet<WidgetPlacement> matchingPlacements = new HashSet<WidgetPlacement>();
            HashSet<WidgetPlacement> filteredPlacements = new HashSet<WidgetPlacement>(); 
            foreach (WidgetPlacement placement in existingPlacements)
            {
                matchingPlacements.Clear();
                foreach (WidgetPlacement otherPlacement in existingPlacements)
                {
                    if (placement.isTooCloseButNotEqualTo(otherPlacement) &&
                        placement.pointSameWayAs(otherPlacement))
                    {
                        matchingPlacements.Add(otherPlacement);
                    }

                }
                placement.mergeWith(matchingPlacements);
                filteredPlacements.Add(placement); 
            }
            return filteredPlacements; 
        }
    }
}
