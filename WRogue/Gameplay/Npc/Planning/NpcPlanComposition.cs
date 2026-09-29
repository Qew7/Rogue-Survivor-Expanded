using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcPlanComposition
    {
        public static void Build(NpcPlanDomain domain)
        {
            NpcPlanningState needed = domain.Desired;
            var selected = new List<NpcOperatorSource>();
            // Follow registered effects back to prerequisites. Cycles cannot add a provider twice.
            for (int pass = 0; pass < domain.Catalog.OperatorSources.Count; pass++)
            {
                bool changed = false;
                foreach (NpcOperatorSource source in domain.Catalog.OperatorSources)
                {
                    if (selected.Contains(source) || (source.Available != null && !source.Available(domain))) continue;
                    if ((source.Produces(domain.Catalog) & needed & ~domain.InitialState).Empty) continue;
                    selected.Add(source); changed = true;
                    if (source.Requires != null) needed |= source.Requires(domain.Catalog);
                }
                if (!changed) break;
            }
            for (int i = selected.Count - 1; i >= 0; i--) selected[i].Bind(domain);
        }
    }
}
