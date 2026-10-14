using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Add content here through registry calls. Neither Actor nor the resolution loop
    // needs a branch for an individual trait or memory.
    static class PersonalityContent
    {
        static TraitEffect E(NpcPersonalityAxis kind, int amount) { return new TraitEffect(kind, amount); }

        static void T(PersonalityRegistry r, string id, string name, NpcPersonalityAxis axis, int amount,
            NpcPersonalityAxis secondAxis, int secondAmount)
        {
            r.Register(new TraitDefinition(id, name, false, false, null,
                E(axis, amount), E(secondAxis, secondAmount)));
        }

        static void A(PersonalityRegistry r, string id, string name, string requires,
            NpcPersonalityAxis axis, int amount, NpcPersonalityAxis secondAxis, int secondAmount)
        {
            r.Register(new TraitDefinition(id, name, true, false, requires,
                E(axis, amount), E(secondAxis, secondAmount)));
        }

        static MemoryOutcome Gain(string trait, Func<Actor, MemoryInstance, bool> when)
        {
            return new MemoryOutcome(when, trait, null);
        }

        static MemoryOutcome Learn(Skills.IDs skill)
        {
            return new MemoryOutcome(null, null, skill);
        }

        static bool Has(Actor actor, string id) { return actor.Personality.HasTrait(id); }

        static bool SawSince(Actor actor, MemoryInstance memory, string kind, int turn)
        {
            if (memory.HasEvidenceSince(kind, turn)) return true;
            foreach (ObservedEvent e in actor.Personality.Events)
                if (e.Kind == kind && e.Turn >= turn) return true;
            return false;
        }

        static bool SawRelatedDeath(Actor actor, Guid subjectId)
        {
            foreach (MemoryInstance memory in actor.Personality.Memories)
                if (memory.SubjectId == subjectId && memory.RelatedToSubject) return true;
            foreach (ObservedEvent e in actor.Personality.Events)
                if (e.Kind == "death" && e.SubjectId == subjectId && e.RelatedToSubject) return true;
            return false;
        }

        static void M(PersonalityRegistry r, string id, string name, string eventKind,
            Func<Actor, SignificantEvent, bool> trigger, MemoryOutcome first,
            MemoryOutcome second, Skills.IDs fallback, Skills.IDs alternative,
            string evidenceKind = null, MemoryRelationRole person = MemoryRelationRole.None,
            int feeling = 0, MemoryRelationRole group = MemoryRelationRole.None,
            int? fallbackFeeling = null, string earnedTrait = null,
            NpcPersonalityAxis? axis = null, int shift = 0)
        {
            MemoryOutcome[] outcomes = earnedTrait == null ?
                new[] { first, second, Learn(fallback), Learn(alternative) } :
                new[] { first, second, Gain(earnedTrait, null), Learn(fallback), Learn(alternative) };
            MemoryDefinition memory = new MemoryDefinition(id, name, 2, 2,
                new[] { new MemoryTrigger(eventKind, trigger) },
                evidenceKind == null ? new string[0] : new[] { evidenceKind }, outcomes).Relate(person, feeling, group,
                fallbackFeeling);
            if (axis.HasValue) memory.ChangeDisposition(axis.Value, shift);
            r.Register(memory, true);
        }

        public static PersonalityRegistry Create(params INpcContentModule[] additional)
        {
            PersonalityRegistry r = new PersonalityRegistry();
            // Fifty starting traits. Effects use common decision axes, not fifty AI branches.
            T(r, "kind", "Kind", NpcPersonalityAxis.Compassion, 25, NpcPersonalityAxis.Group, 5);
            T(r, "cruel", "Cruel", NpcPersonalityAxis.Compassion, -25, NpcPersonalityAxis.Courage, 8);
            T(r, "lawful", "Law-abiding", NpcPersonalityAxis.Law, 25, NpcPersonalityAxis.Group, 5);
            T(r, "rebellious", "Rebellious", NpcPersonalityAxis.Law, -20, NpcPersonalityAxis.Group, -5);
            T(r, "brave", "Brave", NpcPersonalityAxis.Courage, 25, NpcPersonalityAxis.Explore, 8);
            T(r, "timid", "Timid", NpcPersonalityAxis.Courage, -25, NpcPersonalityAxis.Explore, -8);
            T(r, "sociable", "Sociable", NpcPersonalityAxis.Group, 25, NpcPersonalityAxis.Trade, 5);
            T(r, "solitary", "Solitary", NpcPersonalityAxis.Group, -25, NpcPersonalityAxis.Explore, 5);
            T(r, "loyal", "Loyal", NpcPersonalityAxis.Group, 20, NpcPersonalityAxis.Compassion, 8);
            T(r, "independent", "Independent", NpcPersonalityAxis.Group, -15, NpcPersonalityAxis.Courage, 5);
            T(r, "generous", "Generous", NpcPersonalityAxis.Trade, 20, NpcPersonalityAxis.Compassion, 10);
            T(r, "selfish", "Selfish", NpcPersonalityAxis.Trade, -20, NpcPersonalityAxis.Compassion, -10);
            T(r, "frugal", "Frugal", NpcPersonalityAxis.Supplies, 20, NpcPersonalityAxis.Trade, -5);
            T(r, "impulsive", "Impulsive", NpcPersonalityAxis.Courage, 10, NpcPersonalityAxis.Supplies, -8);
            T(r, "patient", "Patient", NpcPersonalityAxis.Courage, -5, NpcPersonalityAxis.Law, 5);
            T(r, "vigilant", "Vigilant", NpcPersonalityAxis.Courage, -8, NpcPersonalityAxis.Supplies, 10);
            T(r, "careless", "Careless", NpcPersonalityAxis.Courage, 8, NpcPersonalityAxis.Supplies, -10);
            T(r, "curious", "Curious", NpcPersonalityAxis.Explore, 25, NpcPersonalityAxis.Item, 5);
            T(r, "cautious", "Cautious", NpcPersonalityAxis.Courage, -15, NpcPersonalityAxis.Explore, -5);
            T(r, "ambitious", "Ambitious", NpcPersonalityAxis.Group, 10, NpcPersonalityAxis.Explore, 10);
            T(r, "humble", "Humble", NpcPersonalityAxis.Group, 5, NpcPersonalityAxis.Trade, 5);
            T(r, "honest", "Honest", NpcPersonalityAxis.Law, 15, NpcPersonalityAxis.Trade, 8);
            T(r, "deceptive", "Deceptive", NpcPersonalityAxis.Law, -15, NpcPersonalityAxis.Trade, -8);
            T(r, "trusting", "Trusting", NpcPersonalityAxis.Group, 15, NpcPersonalityAxis.Compassion, 8);
            T(r, "suspicious", "Suspicious", NpcPersonalityAxis.Group, -15, NpcPersonalityAxis.Courage, -5);
            T(r, "protective", "Protective", NpcPersonalityAxis.Compassion, 20, NpcPersonalityAxis.Courage, 12);
            T(r, "vindictive", "Vindictive", NpcPersonalityAxis.Courage, 15, NpcPersonalityAxis.Law, -5);
            T(r, "forgiving", "Forgiving", NpcPersonalityAxis.Compassion, 15, NpcPersonalityAxis.Courage, -5);
            T(r, "organized", "Organized", NpcPersonalityAxis.Supplies, 15, NpcPersonalityAxis.Group, 5);
            T(r, "messy", "Messy", NpcPersonalityAxis.Supplies, -12, NpcPersonalityAxis.Group, -3);
            T(r, "hoarder", "Hoarder", NpcPersonalityAxis.Item, 15, NpcPersonalityAxis.Supplies, 12);
            T(r, "minimalist", "Minimalist", NpcPersonalityAxis.Item, -10, NpcPersonalityAxis.Supplies, -5);
            T(r, "healer", "Healer at heart", NpcPersonalityAxis.Compassion, 20, NpcPersonalityAxis.Supplies, 8);
            T(r, "scavenger", "Scavenger", NpcPersonalityAxis.Item, 12, NpcPersonalityAxis.Explore, 12);
            T(r, "homebody", "Homebody", NpcPersonalityAxis.Explore, -20, NpcPersonalityAxis.Group, 8);
            T(r, "wanderer", "Wanderer", NpcPersonalityAxis.Explore, 20, NpcPersonalityAxis.Group, -5);
            T(r, "peacemaker", "Peacemaker", NpcPersonalityAxis.Courage, -12, NpcPersonalityAxis.Compassion, 12);
            T(r, "hotheaded", "Hotheaded", NpcPersonalityAxis.Courage, 20, NpcPersonalityAxis.Law, -5);
            T(r, "pragmatic", "Pragmatic", NpcPersonalityAxis.Supplies, 12, NpcPersonalityAxis.Compassion, -5);
            T(r, "idealist", "Idealist", NpcPersonalityAxis.Law, 12, NpcPersonalityAxis.Compassion, 12);
            T(r, "thrillseeker", "Thrill seeker", NpcPersonalityAxis.Courage, 15, NpcPersonalityAxis.Explore, 15);
            T(r, "fearful", "Fearful", NpcPersonalityAxis.Courage, -20, NpcPersonalityAxis.Group, 8);
            T(r, "disciplined", "Disciplined", NpcPersonalityAxis.Supplies, 15, NpcPersonalityAxis.Law, 8);
            T(r, "stubborn", "Stubborn", NpcPersonalityAxis.Group, -8, NpcPersonalityAxis.Courage, 10);
            T(r, "adaptable", "Adaptable", NpcPersonalityAxis.Explore, 8, NpcPersonalityAxis.Group, 8);
            T(r, "devout", "Devout", NpcPersonalityAxis.Law, 8, NpcPersonalityAxis.Compassion, 8);
            T(r, "skeptic", "Skeptic", NpcPersonalityAxis.Group, -8, NpcPersonalityAxis.Law, -5);
            T(r, "opportunist", "Opportunist", NpcPersonalityAxis.Item, 8, NpcPersonalityAxis.Trade, -8);
            r.Register(new TraitDefinition("likes_items", "Likes", false, true, null, E(NpcPersonalityAxis.Item, 35)) { Interest = new ItemPreferenceInterest() });
            r.Register(new TraitDefinition("dislikes_items", "Dislikes", false, true, null, E(NpcPersonalityAxis.Item, -35)));
            r.Conflict("kind", "cruel");
            r.Conflict("lawful", "rebellious");
            r.Conflict("brave", "timid");
            r.Conflict("sociable", "solitary");
            r.Conflict("generous", "selfish");
            r.Conflict("trusting", "suspicious");
            r.Conflict("organized", "messy");
            r.Conflict("homebody", "wanderer");
            r.Conflict("peacemaker", "hotheaded");
            r.Conflict("devout", "skeptic");

            // Advanced traits can only be gained by resolving memories, and need a base trait.
            A(r, "maniac", "Maniac", "cruel", NpcPersonalityAxis.Courage, 35, NpcPersonalityAxis.Compassion, -20);
            A(r, "cannibal", "Cannibal", "pragmatic", NpcPersonalityAxis.Supplies, 30, NpcPersonalityAxis.Law, -30);
            A(r, "kleptomaniac", "Kleptomaniac", "opportunist", NpcPersonalityAxis.Item, 35, NpcPersonalityAxis.Law, -20);
            A(r, "zealot", "Zealous justice", "lawful", NpcPersonalityAxis.Law, 40, NpcPersonalityAxis.Courage, 15);
            A(r, "panic_attacks", "Panic attacks", "fearful", NpcPersonalityAxis.Courage, -40, NpcPersonalityAxis.Explore, -20);
            A(r, "paranoid", "Paranoid", "suspicious", NpcPersonalityAxis.Group, -30, NpcPersonalityAxis.Courage, -15);
            A(r, "berserker", "Berserker", "hotheaded", NpcPersonalityAxis.Courage, 40, NpcPersonalityAxis.Compassion, -15);
            A(r, "selfless", "Selfless", "generous", NpcPersonalityAxis.Compassion, 35, NpcPersonalityAxis.Trade, 15);
            A(r, "hardened", "Hardened", "brave", NpcPersonalityAxis.Courage, 30, NpcPersonalityAxis.Explore, 10);
            A(r, "mistrustful", "Mistrustful", "skeptic", NpcPersonalityAxis.Group, -35, NpcPersonalityAxis.Trade, -15);
            A(r, "obsessive_collector", "Obsessive collector", "hoarder", NpcPersonalityAxis.Item, 35, NpcPersonalityAxis.Supplies, 10);
            A(r, "traumatized", "Traumatized", "timid", NpcPersonalityAxis.Courage, -30, NpcPersonalityAxis.Group, -15);
            A(r, "vengeful", "Vengeful", "vindictive", NpcPersonalityAxis.Courage, 35, NpcPersonalityAxis.Law, -10);
            A(r, "resolute", "Resolute", "disciplined", NpcPersonalityAxis.Courage, 20, NpcPersonalityAxis.Group, 10);
            A(r, "protector", "Protector", "protective", NpcPersonalityAxis.Compassion, 30, NpcPersonalityAxis.Courage, 20);
            A(r, "hermit", "Hermit", "solitary", NpcPersonalityAxis.Group, -40, NpcPersonalityAxis.Explore, -10);
            A(r, "fanatic", "Fanatic", "devout", NpcPersonalityAxis.Law, 25, NpcPersonalityAxis.Courage, 25);
            A(r, "predator", "Predator", "selfish", NpcPersonalityAxis.Courage, 30, NpcPersonalityAxis.Compassion, -30);
            A(r, "survivor", "Survivor", "adaptable", NpcPersonalityAxis.Supplies, 25, NpcPersonalityAxis.Courage, 15);
            A(r, "pacifist", "Pacifist", "peacemaker", NpcPersonalityAxis.Courage, -30, NpcPersonalityAxis.Compassion, 25);
            r.Register(new TraitDefinition("battle_scarred", "Battle scarred", true, false, null,
                E(NpcPersonalityAxis.Courage, -8), E(NpcPersonalityAxis.Supplies, 5)).AsEarnedOnly());
            r.Register(new TraitDefinition("scarcity_hardened", "Scarcity hardened", true, false, null,
                E(NpcPersonalityAxis.Supplies, 12), E(NpcPersonalityAxis.Trade, -5)).AsEarnedOnly());
            r.Trait("patient").WithEffect(NpcPersonalityAxis.Patience, 35);
            r.Trait("forgiving").WithEffect(NpcPersonalityAxis.Patience, 15);
            r.Trait("disciplined").WithEffect(NpcPersonalityAxis.Patience, 15);
            r.Trait("peacemaker").WithEffect(NpcPersonalityAxis.Patience, 20);
            r.Trait("impulsive").WithEffect(NpcPersonalityAxis.Patience, -25);
            r.Trait("hotheaded").WithEffect(NpcPersonalityAxis.Patience, -40);
            r.Trait("vindictive").WithEffect(NpcPersonalityAxis.Patience, -20);
            r.Trait("berserker").WithEffect(NpcPersonalityAxis.Patience, -25);

            // Memory outcomes are ordered: specific paths first, general skill last.
            M(r, "leader_loss", "Loss of a leader", "death", (a,e) => e.Subject == a.Leader,
                Gain("vengeful", (a,m) => Has(a,"vindictive") && SawSince(a,m,"murder",m.StartTurn)),
                Gain("traumatized", (a,m) => Has(a,"timid")), Skills.IDs.LEADERSHIP, Skills.IDs.STRONG_PSYCHE,
                "murder", person: MemoryRelationRole.Subject, feeling: 10, axis: NpcPersonalityAxis.HumanLife, shift: 10);
            M(r, "follower_loss", "Loss of a follower", "death", (a,e) => e.Subject != null && e.Subject.Leader == a,
                Gain("protector", (a,m) => Has(a,"protective")),
                Gain("resolute", (a,m) => Has(a,"disciplined")), Skills.IDs.LEADERSHIP, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Subject, feeling: 10, axis: NpcPersonalityAxis.HumanLife, shift: 10);
            M(r, "witnessed_murder", "Witnessed a murder", "murder", (a,e) => a != e.Subject && a != e.Other,
                Gain("zealot", (a,m) => Has(a,"lawful")),
                Gain("panic_attacks", (a,m) => Has(a,"fearful")), Skills.IDs.STRONG_PSYCHE, Skills.IDs.UNSUSPICIOUS,
                person: MemoryRelationRole.OtherOrSubject, feeling: -30, fallbackFeeling: 10, axis: NpcPersonalityAxis.HumanLife, shift: 8);
            M(r, "killed_person", "Killed a person", "kill_human", (a,e) => a == e.Other,
                Gain("maniac", (a,m) => Has(a,"cruel") && SawSince(a,m,"kill_human",m.StartTurn + 1)),
                Gain("pacifist", (a,m) => Has(a,"peacemaker")), Skills.IDs.MARTIAL_ARTS, Skills.IDs.STRONG_PSYCHE,
                "kill_human", person: MemoryRelationRole.Subject, axis: NpcPersonalityAxis.HumanLife, shift: -12);
            M(r, "survived_attack", "Survived an attack", "attack", (a,e) => a == e.Subject,
                Gain("berserker", (a,m) => Has(a,"hotheaded")),
                Gain("hardened", (a,m) => Has(a,"brave")), Skills.IDs.TOUGH, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Other, feeling: -35, earnedTrait: "battle_scarred");
            M(r, "base_theft", "Theft from home", "base_theft", (a,e) => a != e.Subject && e.Other != null && (a == e.Other || a.Leader == e.Other),
                Gain("kleptomaniac", (a,m) => Has(a,"opportunist")),
                Gain("paranoid", (a,m) => Has(a,"suspicious")), Skills.IDs.UNSUSPICIOUS, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Subject, feeling: -30, axis: NpcPersonalityAxis.Scarcity, shift: 10);
            M(r, "base_loss", "Lost a base", "base_loss", (a,e) => e.Subject != null && (a == e.Subject || a.Leader == e.Subject),
                Gain("hermit", (a,m) => Has(a,"solitary")),
                Gain("survivor", (a,m) => Has(a,"adaptable")), Skills.IDs.CARPENTRY, Skills.IDs.HAULER,
                person: MemoryRelationRole.Subject, group: MemoryRelationRole.Subject, axis: NpcPersonalityAxis.Scarcity, shift: 12);
            M(r, "abandoned", "Abandoned by a group", "abandoned", (a,e) => a == e.Subject,
                Gain("mistrustful", (a,m) => Has(a,"skeptic")),
                Gain("traumatized", (a,m) => Has(a,"timid")), Skills.IDs.STRONG_PSYCHE, Skills.IDs.CHARISMATIC,
                person: MemoryRelationRole.Other, feeling: -35, group: MemoryRelationRole.Other, axis: NpcPersonalityAxis.CareCircle, shift: -12);
            M(r, "new_group", "Found companions", "joined_group", (a,e) => a == e.Subject,
                Gain("selfless", (a,m) => Has(a,"generous")),
                Gain("protector", (a,m) => Has(a,"protective")), Skills.IDs.CHARISMATIC, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Other, feeling: 15, group: MemoryRelationRole.Other, axis: NpcPersonalityAxis.CareCircle, shift: 10);
            M(r, "received_help", "Received help", "helped", (a,e) => a == e.Subject,
                Gain("selfless", (a,m) => Has(a,"generous")),
                Gain("resolute", (a,m) => Has(a,"disciplined")), Skills.IDs.MEDIC, Skills.IDs.CHARISMATIC,
                person: MemoryRelationRole.Other, feeling: 30, axis: NpcPersonalityAxis.CareCircle, shift: 10);
            M(r, "raid", "Survived a raid", "raid", (a,e) => true,
                Gain("fanatic", (a,m) => Has(a,"devout")),
                Gain("predator", (a,m) => Has(a,"selfish")), Skills.IDs.FIREARMS, Skills.IDs.TOUGH);
            M(r, "starvation", "Faced starvation", "starvation", (a,e) => a == e.Subject,
                Gain("cannibal", (a,m) => Has(a,"pragmatic")),
                Gain("survivor", (a,m) => Has(a,"adaptable")), Skills.IDs.LIGHT_EATER, Skills.IDs.STRONG_PSYCHE,
                earnedTrait: "scarcity_hardened", axis: NpcPersonalityAxis.Scarcity, shift: 15);
            M(r, "stockpile", "Lost supplies", "supplies_lost", (a,e) => a == e.Subject,
                Gain("obsessive_collector", (a,m) => Has(a,"hoarder")),
                Gain("paranoid", (a,m) => Has(a,"suspicious")), Skills.IDs.HAULER, Skills.IDs.CARPENTRY,
                person: MemoryRelationRole.Other, feeling: -20, axis: NpcPersonalityAxis.Scarcity, shift: 12);
            M(r, "zombified_friend", "Saw a friend turn", "zombified", (a,e) => e.Other != null &&
                (e.Other.Leader == a || a.Leader == e.Other || SawRelatedDeath(a, e.Other.PersonalityIdentity)),
                Gain("panic_attacks", (a,m) => Has(a,"fearful")),
                Gain("hardened", (a,m) => Has(a,"brave")), Skills.IDs.NECROLOGY, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Other, feeling: 10);
            PersonalityWorldContent.Register(r);
            r.Content = NpcContentDefaults.Create(r, additional);
            return r;
        }
    }
}
