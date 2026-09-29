using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum DecisionKind { Item, Courage, Group, Law, Trade, Explore, Compassion, Supplies }

    // Definitions and effects are registered once; instances keep only IDs and parameters.
    sealed class TraitEffect
    {
        public readonly DecisionKind Decision;
        public readonly int Amount;
        public readonly Func<Actor, Item, bool> Applies;

        public TraitEffect(DecisionKind decision, int amount, Func<Actor, Item, bool> applies = null)
        {
            Decision = decision;
            Amount = amount;
            Applies = applies;
        }
    }

    sealed class TraitDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool Advanced;
        public readonly bool ItemParameter;
        public readonly string RequiresTrait;
        public readonly TraitEffect[] Effects;
        public INpcTraitInterest Interest { get; set; }
        public int RelationFactionId { get; private set; } = -1;
        public int RelationBias { get; private set; }
        readonly List<string> m_Conflicts = new List<string>();

        public TraitDefinition(string id, string name, bool advanced, bool itemParameter,
            string requiresTrait, params TraitEffect[] effects)
        {
            Id = id;
            Name = name;
            Advanced = advanced;
            ItemParameter = itemParameter;
            RequiresTrait = requiresTrait;
            Effects = effects;
        }

        public bool Eligible(Actor actor)
        {
            if (actor.Personality == null || actor.Personality.HasTrait(Id) ||
                (RequiresTrait != null && !actor.Personality.HasTrait(RequiresTrait))) return false;
            foreach (string conflict in m_Conflicts)
                if (actor.Personality.HasTrait(conflict)) return false;
            return true;
        }

        public void AddConflict(string id) { m_Conflicts.Add(id); }
        public TraitDefinition TowardFaction(int id, int amount)
        { RelationFactionId = id; RelationBias = amount; return this; }
    }
}
