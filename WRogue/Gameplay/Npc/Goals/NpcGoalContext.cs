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
        public readonly int Turn, MaxHP, FoodUnits;
        public readonly int GroupBias, CompassionBias, TradeBias, CourageBias, LawBias, SuppliesBias;
        public readonly int HumanLife, CareCircle, MessageTrust, Scarcity, Patience;
        public readonly bool Hungry, HasFood;
        public readonly bool HasActiveGeneratedGoal;
        public readonly NpcContentCatalog Catalog;
        public NpcGoalContext(RogueGame game, Actor owner, NpcContentCatalog catalog)
        {
            Action<string, long> profile = RogueGame.ProfileSimulationPhase;
            long started = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            Owner = owner; Catalog = catalog; Turn = owner.Location.Map.LocalTime.TurnCounter;
            // Sources only propose candidates; the lifecycle changes intents after evaluation.
            foreach (NpcIntent intent in owner.Personality.IntentList)
                if (!intent.Finished && intent.Generated != null)
                { HasActiveGeneratedGoal = true; break; }
            // Goal sources read owner state; actor-only trait biases are shared by this evaluation pass.
            PersonalitySystem.GoalBiases(owner, catalog.Personalities, out GroupBias, out CompassionBias,
                out TradeBias, out CourageBias, out LawBias, out SuppliesBias);
            PersonalitySystem.GoalDispositions(owner, catalog, out HumanLife, out CareCircle,
                out MessageTrust, out Scarcity, out Patience);
            if (profile != null) profile("context traits", System.Diagnostics.Stopwatch.GetTimestamp() - started);
            started = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            MaxHP = game.Rules.ActorMaxHPs(owner); Hungry = game.Rules.IsActorHungry(owner); HasFood = NpcFoodSupply.HasFood(game, owner);
            foreach (Item item in owner.Inventory.Items) if (item is djack.RogueSurvivor.Engine.Items.ItemFood && !game.Rules.IsFoodSpoiled((djack.RogueSurvivor.Engine.Items.ItemFood)item, Turn)) FoodUnits += item.Quantity;
            if (profile != null) profile("context inventory", System.Diagnostics.Stopwatch.GetTimestamp() - started);
            started = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            Self = new NpcKnownPerson { Id = owner.PersonalityIdentity, Name = owner.UnmodifiedName, Place = owner.Location, SeenTurn = Turn };
            var people = new List<NpcKnownPerson>(owner.Personality.Knowledge.People); people.Sort((a, b) => a.Id.CompareTo(b.Id)); People = people.AsReadOnly();
            if (profile != null) profile("context people", System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }
        public NpcKnownPerson FindPerson(Predicate<NpcKnownPerson> match) { foreach (NpcKnownPerson person in People) if (match(person)) return person; return null; }
        public bool AnyPerson(Predicate<NpcKnownPerson> match) { return FindPerson(match) != null; }
        public bool Pending(NpcGoalValue value, Guid subject) { return HasActiveGeneratedGoal && Pending(Catalog.Value(value).Id, subject); }
        public bool Pending(string id, Guid subject)
        {
            if (!HasActiveGeneratedGoal) return false;
            Action<string, long> profile = RogueGame.ProfileSimulationPhase;
            long start = profile == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            foreach (NpcIntent intent in Owner.Personality.IntentList)
                if (!intent.Finished && intent.Generated != null && intent.Generated.SubjectId == subject)
                { NpcValueDefinition definition = Catalog.Value(intent.Generated); if (definition != null && definition.Id == id)
                    { if (profile != null) profile("goal pending", System.Diagnostics.Stopwatch.GetTimestamp() - start); return true; } }
            if (profile != null) profile("goal pending", System.Diagnostics.Stopwatch.GetTimestamp() - start);
            return false;
        }
        public static int Distance(Location a, Location b)
        { return a.Map != b.Map ? 12 : Math.Max(Math.Abs(a.Position.X - b.Position.X), Math.Abs(a.Position.Y - b.Position.Y)); }
    }

    struct NpcMotivation
    {
        public readonly Actor Owner;
        public readonly int CommunityCare, CommunitySecurity;
        public readonly int Group, Compassion, Trade, Courage, Law, Supplies, Feeling, Attachment, Fear, Grievance;
        public readonly int HumanLife, CareCircle, MessageTrust, Scarcity, Patience;
        public readonly bool Close;
        public NpcMotivation(NpcContentCatalog catalog, Actor owner, Guid subject, string excludeTrait = null,
            NpcGoalContext context = null, NpcKnownPerson knownPerson = null)
        {
            this = default(NpcMotivation);
            if (excludeTrait != null) context = null;
            PersonalityRegistry registry = catalog.Personalities;
            NpcKnownPerson known = knownPerson ?? owner.Personality.Knowledge.Person(subject);
            if (known != null && owner.Faction != null && known.FactionId == owner.Faction.ID) {
                NpcFactionPolicy policy = catalog.FactionPolicy(owner.Faction.ID);
                CommunityCare = policy.Care; CommunitySecurity = policy.Security;
            }
            Owner = owner;
            Group = context == null ? PersonalitySystem.Bias(owner, NpcPersonalityAxis.Group, registry: registry, excludeTrait: excludeTrait) : context.GroupBias;
            Compassion = context == null ? PersonalitySystem.Bias(owner, NpcPersonalityAxis.Compassion, registry: registry, excludeTrait: excludeTrait) : context.CompassionBias;
            Trade = context == null ? PersonalitySystem.Bias(owner, NpcPersonalityAxis.Trade, registry: registry, excludeTrait: excludeTrait) : context.TradeBias;
            Courage = context == null ? PersonalitySystem.Bias(owner, NpcPersonalityAxis.Courage, registry: registry, excludeTrait: excludeTrait) : context.CourageBias;
            Law = context == null ? PersonalitySystem.Bias(owner, NpcPersonalityAxis.Law, registry: registry, excludeTrait: excludeTrait) : context.LawBias;
            Supplies = context == null ? PersonalitySystem.Bias(owner, NpcPersonalityAxis.Supplies, registry: registry, excludeTrait: excludeTrait) : context.SuppliesBias;
            HumanLife = context == null ? PersonalitySystem.Disposition(owner, NpcPersonalityAxis.HumanLife, catalog, excludeTrait) : context.HumanLife;
            CareCircle = context == null ? PersonalitySystem.Disposition(owner, NpcPersonalityAxis.CareCircle, catalog, excludeTrait) : context.CareCircle;
            MessageTrust = context == null ? PersonalitySystem.Disposition(owner, NpcPersonalityAxis.MessageTrust, catalog, excludeTrait) : context.MessageTrust;
            Scarcity = context == null ? PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Scarcity, catalog, excludeTrait) : context.Scarcity;
            Patience = context == null ? PersonalitySystem.Disposition(owner, NpcPersonalityAxis.Patience, catalog, excludeTrait) : context.Patience;
            RelationshipRecord opinion;
            Feeling = NpcRelationshipValue.Calculate(owner, subject, known == null ? Guid.Empty : known.GroupId,
                known == null ? -1 : known.FactionId, out opinion, true, registry, excludeTrait);
            Close = owner.SocialGroup != null && known != null && known.GroupId == owner.SocialGroup.Identity ||
                opinion != null && opinion.Attachment >= 20;
            if (opinion != null) { Attachment = opinion.Attachment; Fear = opinion.Fear; Grievance = opinion.Grievance; }
        }
    }
}
