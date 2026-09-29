using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class NutritionModule
    {
        static void PerceiveItems(NpcPerceptionContext c)
        {
            RogueGame game = c.Game; Actor actor = c.Owner; Inventory items = c.Items;
            Location place = c.Place; int turn = c.Turn, risk = c.Risk;
            NpcKnowledge knowledge = actor.Personality.Knowledge;
            int units = 0;
                    foreach (Item item in items.Items) if (item is ItemFood && !game.Rules.IsFoodSpoiled((ItemFood)item, turn)) units += item.Quantity;
                    NpcKnownPlace old = knowledge.Places.Find(p => p.Kind == "food" && p.Place == place);
                    if (units > 0 && (old == null || old.Units != units || turn - old.SeenTurn > 180))
                        knowledge.Learn(new NpcFact { Kind = "food_cache", EventId = Session.Get.NextPersonalityEventId(), EventTurn = turn, LearnedTurn = turn,
                            Confidence = 90, Source = NpcKnowledgeSource.Witness, SourceId = actor.PersonalityIdentity, Place = place, Units = units, Risk = risk });
                    knowledge.RememberPlace(new NpcKnownPlace(place, "food", turn, units, risk));
        }
    }
}
