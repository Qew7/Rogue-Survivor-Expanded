using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcMedicalObservation
    {
        public static void Perceive(RogueGame game, Actor owner, Actor person, NpcKnownPerson known, int turn)
        {
            int wounds = Math.Max(0, game.Rules.ActorMaxHPs(person) - person.HitPoints) * 100 / game.Rules.ActorMaxHPs(person);
            if (wounds == 0 && known.Wounds > 0 || wounds > known.Wounds || turn - known.MedicalTurn > 60)
            { known.MedicalNeed = wounds; known.MedicalConfidence = 90; known.MedicalTurn = turn; }
            known.Wounds = wounds;
        }
    }
}
