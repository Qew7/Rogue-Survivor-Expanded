using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class NutritionModule
    {
        static void HearNeed(NpcReportContext c)
        {
            Actor owner = c.Listener; NpcFact fact = c.Fact; NpcKnowledge knowledge = owner.Personality.Knowledge;
            NpcKnownPerson subject = fact.NamesSubject ? knowledge.Person(fact.SubjectId) : null;
            if (fact.Kind == "requested_food" && subject != null && (fact.EventTurn > subject.FoodNeedTurn ||
                (fact.EventTurn == subject.FoodNeedTurn && fact.Confidence > subject.FoodConfidence)))
            { subject.FoodNeed = 100; subject.FoodNeedTurn = fact.EventTurn; subject.FoodConfidence = fact.Confidence;
                subject.NeedCause = fact.EventId; subject.NeedStory = fact.StoryId; }
        }
    }
}
