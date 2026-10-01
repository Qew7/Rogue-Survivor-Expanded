using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcIntentSystem
    {
        public static bool Enabled(Actor actor)
        { return Session.Get.GamePreset.NpcPersonalitiesEnabled && actor != null && !actor.IsPlayer && !actor.IsDead &&
            actor.Personality != null && actor.Model != null && actor.Controller is Gameplay.AI.OrderableAI &&
            actor.Model.Abilities.IsIntelligent && !actor.Model.Abilities.IsUndead; }
        public static void Finish(NpcContentCatalog catalog, Actor owner, NpcIntent intent,
            NpcIntentStatus status, string reason)
        { NpcGoalLifecycle.Finish(catalog, owner, intent, status, reason); }

    }
}
