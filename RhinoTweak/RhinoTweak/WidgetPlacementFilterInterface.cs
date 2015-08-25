using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RhinoTweak
{
    interface WidgetPlacementFilterInterface
    {

        HashSet<WidgetPlacement> filter(HashSet<WidgetPlacement> existingPlacements); 
    }
}
