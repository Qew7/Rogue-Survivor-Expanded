using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Goal sources receive the owner's needs and retained beliefs, never a world or town index.
    sealed class NpcGoalContext
    {
        public readonly Actor Owner;
        public readonly NpcKnownPerson Self;
        public readonly IList<NpcKnownPerson> People;
        public readonly int Turn, MaxHP;
        public readonly bool Hungry, HasFood;
        public readonly NpcContentCatalog Catalog;
        public NpcGoalContext(RogueGame game, Actor owner, NpcContentCatalog catalog)
        {
            Owner = owner; Catalog = catalog; Turn = owner.Location.Map.LocalTime.TurnCounter;
            MaxHP = game.Rules.ActorMaxHPs(owner); Hungry = game.Rules.IsActorHungry(owner); HasFood = NpcFoodSupply.HasFood(game, owner);
            Self = new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location, SeenTurn = Turn };
            var people = new List<NpcKnownPerson>(owner.Personality.Knowledge.People); people.Sort((a, b) => a.Id.CompareTo(b.Id)); People = people.AsReadOnly();
        }
        public NpcKnownPerson FindPerson(Predicate<NpcKnownPerson> match) { foreach (NpcKnownPerson person in People) if (match(person)) return person; return null; }
        public bool AnyPerson(Predicate<NpcKnownPerson> match) { return FindPerson(match) != null; }
        public bool Pending(NpcGoalValue value, Guid subject) { return Pending(Catalog.Value(value).Id, subject); }
        public bool Pending(string id, Guid subject)
        {
            foreach (NpcIntent intent in Owner.Personality.Intents)
                if (!intent.Finished && intent.Generated != null && intent.Generated.SubjectId == subject)
                { NpcValueDefinition definition = Catalog.Value(intent.Generated); if (definition != null && definition.Id == id) return true; }
            return false;
        }
        public static int Distance(Location a, Location b)
        { return a.Map != b.Map ? 12 : Math.Max(Math.Abs(a.Position.X - b.Position.X), Math.Abs(a.Position.Y - b.Position.Y)); }
    }

    sealed class NpcMotivation
    {
        public readonly Actor Owner;
        public readonly int Group, Compassion, Trade, Courage, Law, Supplies, Feeling, Attachment, Fear, Grievance;
        public NpcMotivation(Actor owner, Guid subject, PersonalityRegistry registry = null)
        {
            Owner = owner; Group = PersonalitySystem.Bias(owner, DecisionKind.Group, registry: registry); Compassion = PersonalitySystem.Bias(owner, DecisionKind.Compassion, registry: registry);
            Trade = PersonalitySystem.Bias(owner, DecisionKind.Trade, registry: registry); Courage = PersonalitySystem.Bias(owner, DecisionKind.Courage, registry: registry);
            Law = PersonalitySystem.Bias(owner, DecisionKind.Law, registry: registry); Supplies = PersonalitySystem.Bias(owner, DecisionKind.Supplies, registry: registry);
            Feeling = NpcValues.KnownAttitude(owner, subject, registry); RelationshipRecord opinion = owner.Personality.Person(subject);
            if (opinion != null) { Attachment = opinion.Attachment; Fear = opinion.Fear; Grievance = opinion.Grievance; }
        }
    }
}
