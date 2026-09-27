using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Add content here through registry calls. Neither Actor nor the resolution loop
    // needs a branch for an individual trait or memory.
    static class PersonalityContent
    {
        static TraitEffect E(DecisionKind kind, int amount) { return new TraitEffect(kind, amount); }

        static void T(PersonalityRegistry r, string id, string name, DecisionKind axis, int amount,
            DecisionKind secondAxis, int secondAmount)
        {
            r.Register(new TraitDefinition(id, name, false, false, null,
                E(axis, amount), E(secondAxis, secondAmount)));
        }

        static void A(PersonalityRegistry r, string id, string name, string requires,
            DecisionKind axis, int amount, DecisionKind secondAxis, int secondAmount)
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
            int? fallbackFeeling = null)
        {
            r.Register(new MemoryDefinition(id, name, 2, 6,
                new[] { new MemoryTrigger(eventKind, trigger) },
                evidenceKind == null ? new string[0] : new[] { evidenceKind }, first, second,
                Learn(fallback), Learn(alternative)).Relate(person, feeling, group,
                fallbackFeeling), true);
        }

        public static PersonalityRegistry Create()
        {
            PersonalityRegistry r = new PersonalityRegistry();
            // Fifty starting traits. Effects use common decision axes, not fifty AI branches.
            T(r, "kind", "Kind", DecisionKind.Compassion, 25, DecisionKind.Group, 5);
            T(r, "cruel", "Cruel", DecisionKind.Compassion, -25, DecisionKind.Courage, 8);
            T(r, "lawful", "Law-abiding", DecisionKind.Law, 25, DecisionKind.Group, 5);
            T(r, "rebellious", "Rebellious", DecisionKind.Law, -20, DecisionKind.Group, -5);
            T(r, "brave", "Brave", DecisionKind.Courage, 25, DecisionKind.Explore, 8);
            T(r, "timid", "Timid", DecisionKind.Courage, -25, DecisionKind.Explore, -8);
            T(r, "sociable", "Sociable", DecisionKind.Group, 25, DecisionKind.Trade, 5);
            T(r, "solitary", "Solitary", DecisionKind.Group, -25, DecisionKind.Explore, 5);
            T(r, "loyal", "Loyal", DecisionKind.Group, 20, DecisionKind.Compassion, 8);
            T(r, "independent", "Independent", DecisionKind.Group, -15, DecisionKind.Courage, 5);
            T(r, "generous", "Generous", DecisionKind.Trade, 20, DecisionKind.Compassion, 10);
            T(r, "selfish", "Selfish", DecisionKind.Trade, -20, DecisionKind.Compassion, -10);
            T(r, "frugal", "Frugal", DecisionKind.Supplies, 20, DecisionKind.Trade, -5);
            T(r, "impulsive", "Impulsive", DecisionKind.Courage, 10, DecisionKind.Supplies, -8);
            T(r, "patient", "Patient", DecisionKind.Courage, -5, DecisionKind.Law, 5);
            T(r, "vigilant", "Vigilant", DecisionKind.Courage, -8, DecisionKind.Supplies, 10);
            T(r, "careless", "Careless", DecisionKind.Courage, 8, DecisionKind.Supplies, -10);
            T(r, "curious", "Curious", DecisionKind.Explore, 25, DecisionKind.Item, 5);
            T(r, "cautious", "Cautious", DecisionKind.Courage, -15, DecisionKind.Explore, -5);
            T(r, "ambitious", "Ambitious", DecisionKind.Group, 10, DecisionKind.Explore, 10);
            T(r, "humble", "Humble", DecisionKind.Group, 5, DecisionKind.Trade, 5);
            T(r, "honest", "Honest", DecisionKind.Law, 15, DecisionKind.Trade, 8);
            T(r, "deceptive", "Deceptive", DecisionKind.Law, -15, DecisionKind.Trade, -8);
            T(r, "trusting", "Trusting", DecisionKind.Group, 15, DecisionKind.Compassion, 8);
            T(r, "suspicious", "Suspicious", DecisionKind.Group, -15, DecisionKind.Courage, -5);
            T(r, "protective", "Protective", DecisionKind.Compassion, 20, DecisionKind.Courage, 12);
            T(r, "vindictive", "Vindictive", DecisionKind.Courage, 15, DecisionKind.Law, -5);
            T(r, "forgiving", "Forgiving", DecisionKind.Compassion, 15, DecisionKind.Courage, -5);
            T(r, "organized", "Organized", DecisionKind.Supplies, 15, DecisionKind.Group, 5);
            T(r, "messy", "Messy", DecisionKind.Supplies, -12, DecisionKind.Group, -3);
            T(r, "hoarder", "Hoarder", DecisionKind.Item, 15, DecisionKind.Supplies, 12);
            T(r, "minimalist", "Minimalist", DecisionKind.Item, -10, DecisionKind.Supplies, -5);
            T(r, "healer", "Healer at heart", DecisionKind.Compassion, 20, DecisionKind.Supplies, 8);
            T(r, "scavenger", "Scavenger", DecisionKind.Item, 12, DecisionKind.Explore, 12);
            T(r, "homebody", "Homebody", DecisionKind.Explore, -20, DecisionKind.Group, 8);
            T(r, "wanderer", "Wanderer", DecisionKind.Explore, 20, DecisionKind.Group, -5);
            T(r, "peacemaker", "Peacemaker", DecisionKind.Courage, -12, DecisionKind.Compassion, 12);
            T(r, "hotheaded", "Hotheaded", DecisionKind.Courage, 20, DecisionKind.Law, -5);
            T(r, "pragmatic", "Pragmatic", DecisionKind.Supplies, 12, DecisionKind.Compassion, -5);
            T(r, "idealist", "Idealist", DecisionKind.Law, 12, DecisionKind.Compassion, 12);
            T(r, "thrillseeker", "Thrill seeker", DecisionKind.Courage, 15, DecisionKind.Explore, 15);
            T(r, "fearful", "Fearful", DecisionKind.Courage, -20, DecisionKind.Group, 8);
            T(r, "disciplined", "Disciplined", DecisionKind.Supplies, 15, DecisionKind.Law, 8);
            T(r, "stubborn", "Stubborn", DecisionKind.Group, -8, DecisionKind.Courage, 10);
            T(r, "adaptable", "Adaptable", DecisionKind.Explore, 8, DecisionKind.Group, 8);
            T(r, "devout", "Devout", DecisionKind.Law, 8, DecisionKind.Compassion, 8);
            T(r, "skeptic", "Skeptic", DecisionKind.Group, -8, DecisionKind.Law, -5);
            T(r, "opportunist", "Opportunist", DecisionKind.Item, 8, DecisionKind.Trade, -8);
            r.Register(new TraitDefinition("likes_items", "Likes", false, true, null, E(DecisionKind.Item, 35)));
            r.Register(new TraitDefinition("dislikes_items", "Dislikes", false, true, null, E(DecisionKind.Item, -35)));
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
            A(r, "maniac", "Maniac", "cruel", DecisionKind.Courage, 35, DecisionKind.Compassion, -20);
            A(r, "cannibal", "Cannibal", "pragmatic", DecisionKind.Supplies, 30, DecisionKind.Law, -30);
            A(r, "kleptomaniac", "Kleptomaniac", "opportunist", DecisionKind.Item, 35, DecisionKind.Law, -20);
            A(r, "zealot", "Zealous justice", "lawful", DecisionKind.Law, 40, DecisionKind.Courage, 15);
            A(r, "panic_attacks", "Panic attacks", "fearful", DecisionKind.Courage, -40, DecisionKind.Explore, -20);
            A(r, "paranoid", "Paranoid", "suspicious", DecisionKind.Group, -30, DecisionKind.Courage, -15);
            A(r, "berserker", "Berserker", "hotheaded", DecisionKind.Courage, 40, DecisionKind.Compassion, -15);
            A(r, "selfless", "Selfless", "generous", DecisionKind.Compassion, 35, DecisionKind.Trade, 15);
            A(r, "hardened", "Hardened", "brave", DecisionKind.Courage, 30, DecisionKind.Explore, 10);
            A(r, "mistrustful", "Mistrustful", "skeptic", DecisionKind.Group, -35, DecisionKind.Trade, -15);
            A(r, "obsessive_collector", "Obsessive collector", "hoarder", DecisionKind.Item, 35, DecisionKind.Supplies, 10);
            A(r, "traumatized", "Traumatized", "timid", DecisionKind.Courage, -30, DecisionKind.Group, -15);
            A(r, "vengeful", "Vengeful", "vindictive", DecisionKind.Courage, 35, DecisionKind.Law, -10);
            A(r, "resolute", "Resolute", "disciplined", DecisionKind.Courage, 20, DecisionKind.Group, 10);
            A(r, "protector", "Protector", "protective", DecisionKind.Compassion, 30, DecisionKind.Courage, 20);
            A(r, "hermit", "Hermit", "solitary", DecisionKind.Group, -40, DecisionKind.Explore, -10);
            A(r, "fanatic", "Fanatic", "devout", DecisionKind.Law, 25, DecisionKind.Courage, 25);
            A(r, "predator", "Predator", "selfish", DecisionKind.Courage, 30, DecisionKind.Compassion, -30);
            A(r, "survivor", "Survivor", "adaptable", DecisionKind.Supplies, 25, DecisionKind.Courage, 15);
            A(r, "pacifist", "Pacifist", "peacemaker", DecisionKind.Courage, -30, DecisionKind.Compassion, 25);

            // Memory outcomes are ordered: specific paths first, general skill last.
            M(r, "leader_loss", "Loss of a leader", "death", (a,e) => e.Subject == a.Leader,
                Gain("vengeful", (a,m) => Has(a,"vindictive") && SawSince(a,m,"murder",m.StartTurn)),
                Gain("traumatized", (a,m) => Has(a,"timid")), Skills.IDs.LEADERSHIP, Skills.IDs.STRONG_PSYCHE,
                "murder", person: MemoryRelationRole.Subject, feeling: 10);
            M(r, "follower_loss", "Loss of a follower", "death", (a,e) => e.Subject != null && e.Subject.Leader == a,
                Gain("protector", (a,m) => Has(a,"protective")),
                Gain("resolute", (a,m) => Has(a,"disciplined")), Skills.IDs.LEADERSHIP, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Subject, feeling: 10);
            M(r, "witnessed_murder", "Witnessed a murder", "murder", (a,e) => a != e.Subject && a != e.Other,
                Gain("zealot", (a,m) => Has(a,"lawful")),
                Gain("panic_attacks", (a,m) => Has(a,"fearful")), Skills.IDs.STRONG_PSYCHE, Skills.IDs.UNSUSPICIOUS,
                person: MemoryRelationRole.OtherOrSubject, feeling: -30, fallbackFeeling: 10);
            M(r, "killed_person", "Killed a person", "kill_human", (a,e) => a == e.Other,
                Gain("maniac", (a,m) => Has(a,"cruel") && SawSince(a,m,"kill_human",m.StartTurn + 1)),
                Gain("pacifist", (a,m) => Has(a,"peacemaker")), Skills.IDs.MARTIAL_ARTS, Skills.IDs.STRONG_PSYCHE,
                "kill_human", person: MemoryRelationRole.Subject);
            M(r, "survived_attack", "Survived an attack", "attack", (a,e) => a == e.Subject,
                Gain("berserker", (a,m) => Has(a,"hotheaded")),
                Gain("hardened", (a,m) => Has(a,"brave")), Skills.IDs.TOUGH, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Other, feeling: -35);
            M(r, "base_theft", "Theft from home", "base_theft", (a,e) => a != e.Subject && e.Other != null && (a == e.Other || a.Leader == e.Other),
                Gain("kleptomaniac", (a,m) => Has(a,"opportunist")),
                Gain("paranoid", (a,m) => Has(a,"suspicious")), Skills.IDs.UNSUSPICIOUS, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Subject, feeling: -30);
            M(r, "base_loss", "Lost a base", "base_loss", (a,e) => e.Subject != null && (a == e.Subject || a.Leader == e.Subject),
                Gain("hermit", (a,m) => Has(a,"solitary")),
                Gain("survivor", (a,m) => Has(a,"adaptable")), Skills.IDs.CARPENTRY, Skills.IDs.HAULER,
                person: MemoryRelationRole.Subject, group: MemoryRelationRole.Subject);
            M(r, "abandoned", "Abandoned by a group", "abandoned", (a,e) => a == e.Subject,
                Gain("mistrustful", (a,m) => Has(a,"skeptic")),
                Gain("traumatized", (a,m) => Has(a,"timid")), Skills.IDs.STRONG_PSYCHE, Skills.IDs.CHARISMATIC,
                person: MemoryRelationRole.Other, feeling: -35, group: MemoryRelationRole.Other);
            M(r, "new_group", "Found companions", "joined_group", (a,e) => a == e.Subject,
                Gain("selfless", (a,m) => Has(a,"generous")),
                Gain("protector", (a,m) => Has(a,"protective")), Skills.IDs.CHARISMATIC, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Other, feeling: 15, group: MemoryRelationRole.Other);
            M(r, "received_help", "Received help", "helped", (a,e) => a == e.Subject,
                Gain("selfless", (a,m) => Has(a,"generous")),
                Gain("resolute", (a,m) => Has(a,"disciplined")), Skills.IDs.MEDIC, Skills.IDs.CHARISMATIC,
                person: MemoryRelationRole.Other, feeling: 30);
            M(r, "raid", "Survived a raid", "raid", (a,e) => true,
                Gain("fanatic", (a,m) => Has(a,"devout")),
                Gain("predator", (a,m) => Has(a,"selfish")), Skills.IDs.FIREARMS, Skills.IDs.TOUGH);
            M(r, "starvation", "Faced starvation", "starvation", (a,e) => a == e.Subject,
                Gain("cannibal", (a,m) => Has(a,"pragmatic")),
                Gain("survivor", (a,m) => Has(a,"adaptable")), Skills.IDs.LIGHT_EATER, Skills.IDs.STRONG_PSYCHE);
            M(r, "stockpile", "Lost supplies", "supplies_lost", (a,e) => a == e.Subject,
                Gain("obsessive_collector", (a,m) => Has(a,"hoarder")),
                Gain("paranoid", (a,m) => Has(a,"suspicious")), Skills.IDs.HAULER, Skills.IDs.CARPENTRY,
                person: MemoryRelationRole.Other, feeling: -20);
            M(r, "zombified_friend", "Saw a friend turn", "zombified", (a,e) => e.Other != null &&
                (e.Other.Leader == a || a.Leader == e.Other || SawRelatedDeath(a, e.Other.PersonalityIdentity)),
                Gain("panic_attacks", (a,m) => Has(a,"fearful")),
                Gain("hardened", (a,m) => Has(a,"brave")), Skills.IDs.NECROLOGY, Skills.IDs.STRONG_PSYCHE,
                person: MemoryRelationRole.Other, feeling: 10);
            return r;
        }
    }
}
