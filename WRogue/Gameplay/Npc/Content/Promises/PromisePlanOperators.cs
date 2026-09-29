using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class PromisePlanOperators
    {
        public static void Build(NpcPlanDomain d)
        {
            NpcResourceDefinition resource = d.Catalog.Resource(d.Goal.Generated == null || d.Goal.Generated.Resource == null ? "food" : d.Goal.Generated.Resource);
            if (resource != null && resource.DeliveryPlan != null) resource.DeliveryPlan(d);
        }
    }
}
