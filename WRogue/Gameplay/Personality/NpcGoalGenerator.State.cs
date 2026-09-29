using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static partial class NpcGoalGenerator
    {
        // Inputs are present needs, retained beliefs and relationships; no event templates.
        public static List<NpcGoalCandidate> Evaluate(RogueGame game, Actor owner)
        {
            var result = new List<NpcGoalCandidate>();
            if (!NpcIntentSystem.Enabled(owner)) return result;
            int turn = owner.Location.Map.LocalTime.TurnCounter;
            NpcKnowledge knowledge = owner.Personality.Knowledge;
            var self = new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location, SeenTurn = turn };
            int maxHP = game.Rules.ActorMaxHPs(owner);
            if (owner.HitPoints < maxHP || Pending(owner, NpcGoalValue.Recovery, self.Id))
                Add(result, owner, self, NpcGoalValue.Recovery, NpcIntentContent.Recover, owner.HitPoints, maxHP,
                    Math.Max(0, maxHP - owner.HitPoints) * 100 / Math.Max(1, maxHP), 100);
            var people = new List<NpcKnownPerson>(knowledge.People); people.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (game.Rules.IsActorHungry(owner) || Pending(owner, NpcGoalValue.Nutrition, self.Id))
            {
                NpcKnownPerson listener = null; int best = Int32.MinValue;
                foreach (NpcKnownPerson person in people)
                {
                    if (person.Id == self.Id || person.Hostile || person.Dead || person.SeenTurn != turn || person.Place.Map != owner.Location.Map) continue;
                    int preference = NpcValues.Importance(owner, NpcGoalValue.Belonging, person.Id) - game.Rules.GridDistance(owner.Location.Position, person.Place.Position);
                    if (owner.Leader != null && owner.Leader.PersonalityIdentity == person.Id) preference += 10;
                    if (preference > best) { best = preference; listener = person; }
                }
                bool social = listener != null && 35 + PersonalitySystem.Bias(owner, DecisionKind.Group) + PersonalitySystem.Bias(owner, DecisionKind.Trade) / 2 >= 20;
                int available = NpcIntentSystem.HasFood(game, owner) || !game.Rules.IsActorHungry(owner) ? 100 : 0;
                Add(result, owner, social ? listener : self, NpcGoalValue.Nutrition, social ? NpcIntentContent.Request : NpcIntentContent.Obtain,
                    available, 100, 100 - available, 100);
            }
            foreach (NpcKnownPerson person in people)
            {
                if (person.Id == self.Id || person.Dead) continue;
                RelationshipRecord opinion = owner.Personality.Person(person.Id);
                if (!person.Hostile && (turn - person.FoodNeedTurn <= 60 || Pending(owner, NpcGoalValue.Care, person.Id)))
                    Add(result, owner, person, NpcGoalValue.Care, NpcIntentContent.Help, 100 - person.FoodNeed, 100,
                        turn - person.FoodNeedTurn <= 60 ? person.FoodNeed : 0, person.FoodConfidence, person.NeedCause, person.NeedStory);
                if (!person.Hostile && opinion != null && (turn >= person.ReciprocityTurn || Pending(owner, NpcGoalValue.Reciprocity, person.Id)) &&
                    (opinion.Debt > 0 || Pending(owner, NpcGoalValue.Reciprocity, person.Id)))
                {
                    NpcIntent existing = owner.Personality.IntentList.Find(i => !i.Finished && i.Generated != null &&
                        i.Generated.Value == NpcGoalValue.Reciprocity && i.Generated.SubjectId == person.Id);
                    int desired = existing == null ? Math.Max(0, opinion.Debt - 10) : existing.Generated.Desired;
                    Add(result, owner, person, NpcGoalValue.Reciprocity, NpcIntentContent.Repay, opinion.Debt, desired,
                        Math.Min(100, Math.Max(0, opinion.Debt - desired) * 10), 100, person.SocialCause);
                }
                int danger = turn - person.ThreatTurn <= 180 ? person.Danger : 0;
                int violation = turn - (person.ViolationConfidence == 0 ? person.ThreatTurn : person.ViolationTurn) <= 180 ? person.Violation : 0;
                bool near = person.Place.Map == owner.Location.Map && game.Rules.GridDistance(owner.Location.Position, person.Place.Position) < 5;
                if (danger > 0 && (near || Pending(owner, NpcGoalValue.Safety, person.Id)))
                    Add(result, owner, person, NpcGoalValue.Safety, NpcIntentContent.Avoid, danger, 0, danger, person.ThreatConfidence, person.ThreatCause);
                if (!person.Hostile && violation > 0)
                    Add(result, owner, person, NpcGoalValue.Justice, NpcIntentContent.Confront, violation, 0, violation,
                        person.ViolationConfidence == 0 ? person.ThreatConfidence : person.ViolationConfidence,
                        person.ViolationCause == 0 ? person.ThreatCause : person.ViolationCause);
                if (owner.Leader != null && owner.Leader.PersonalityIdentity == person.Id && danger > 0)
                    Add(result, owner, person, NpcGoalValue.Autonomy, NpcIntentContent.Leave, danger, 0, danger, person.ThreatConfidence, person.ThreatCause);
                if (!person.Hostile && owner.SocialGroup != null && owner.SocialGroup.Members.Contains(person.Id) &&
                    (turn - person.SeenTurn >= 30 || Pending(owner, NpcGoalValue.Belonging, person.Id)))
                    Add(result, owner, person, NpcGoalValue.Belonging, NpcIntentContent.Seek, 0, 100, 100, person.Confidence);
            }
            return result;
        }
    }
}
