using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcOperatorSource
    {
        public readonly string Id;
        public readonly Func<NpcContentCatalog, NpcPlanningState> Produces, Requires;
        public readonly Action<NpcPlanDomain> Bind;
        public readonly Func<NpcPlanDomain, bool> Available;
        public NpcOperatorSource(string id, Func<NpcContentCatalog, NpcPlanningState> produces,
            Action<NpcPlanDomain> bind, Func<NpcContentCatalog, NpcPlanningState> requires = null,
            Func<NpcPlanDomain, bool> available = null)
        { Id = id; Produces = produces; Bind = bind; Requires = requires; Available = available; }
    }
}
