using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class MedicalCareModule
    {
        static void PerceiveItems(NpcPerceptionContext c)
        {
            Actor actor = c.Owner; Inventory items = c.Items;
            Location place = c.Place; int turn = c.Turn, risk = c.Risk;
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            int units = 0;
            foreach (Item item in items.Items)
                if (item is Engine.Items.ItemMedicine && ((Engine.Items.ItemMedicine)item).Healing > 0) units += item.Quantity;
            NpcKnownPlace old = knowledge.Places.Find(p => p.Kind == "medicine" && p.Place == place);
            if (units > 0 && (old == null || old.Units != units || turn - old.SeenTurn > 180))
                knowledge.Learn(new NpcFact { Kind = "medicine_cache", EventId = Session.Get.NextPersonalityEventId(),
                    EventTurn = turn, LearnedTurn = turn, Confidence = 90, Source = NpcKnowledgeSource.Witness,
                    SourceId = actor.PersonalityIdentity, Place = place, Units = units, Risk = risk });
            if (old != null || units > 0) knowledge.RememberPlace(new NpcKnownPlace(place, "medicine", turn, units, risk));
        }
    }
}
