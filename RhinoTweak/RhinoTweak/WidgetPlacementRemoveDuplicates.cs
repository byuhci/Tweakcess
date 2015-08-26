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
            HashSet<WidgetPlacement> newSet = new HashSet<WidgetPlacement>();
            HashSet<WidgetPlacement> oldset = new HashSet<WidgetPlacement>();
            oldset.UnionWith(existingPlacements);
            Boolean keepGoing = true;
            int iterations = 0; 
            while (keepGoing )
            {
                newSet = doTheFiltering(oldset);
                keepGoing = oldset.IsProperSupersetOf(newSet);
                oldset.Clear();
                oldset.UnionWith(newSet);
                newSet.Clear();
                iterations++;
                RhinoLog.debug("iteration " + iterations); 
            }
            return oldset; 
        }

        private HashSet <WidgetPlacement> doTheFiltering (HashSet<WidgetPlacement> existingPlacements)
        {
            RhinoLog.debug("starting with " + existingPlacements.Count + " placements");
            HashSet<WidgetPlacement> matchingPlacements = new HashSet<WidgetPlacement>();
            HashSet<WidgetPlacement> filteredPlacements = new HashSet<WidgetPlacement>();
            List<WidgetPlacement> existingPlacementAsList =
                new List<WidgetPlacement>(existingPlacements);

            while (existingPlacementAsList.Count != 0)
            {
                matchingPlacements.Clear();
                WidgetPlacement placement = existingPlacementAsList[0];
                existingPlacementAsList.RemoveAt(0);
                foreach (WidgetPlacement otherPlacement in existingPlacementAsList)
                {
                    Boolean tooClose = placement.isTooCloseButNotEqualTo(otherPlacement);
                    Boolean pointsSameWay = placement.pointSameWayAs(otherPlacement);
                    Boolean sameKind = placement.sameKindAs(otherPlacement);
                    //RhinoLog.debug(">> tooclose? " + tooClose + " points same way? " + pointsSameWay); 
                    if (tooClose && pointsSameWay)
                    {
                        matchingPlacements.Add(otherPlacement);
                    }
                }
                foreach (WidgetPlacement duplicatedPlacement in matchingPlacements)
                {
                    existingPlacementAsList.Remove(duplicatedPlacement);
                }
                placement.mergeWith(matchingPlacements);
                filteredPlacements.Add(placement);
            }
            RhinoLog.debug("returning " + filteredPlacements.Count);
            return filteredPlacements;
        }
    }
}
