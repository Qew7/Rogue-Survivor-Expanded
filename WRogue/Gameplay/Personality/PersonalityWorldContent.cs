using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class PersonalityWorldContent
    {
        internal sealed class Experience
        {
            public readonly string Kind, Id, Name, Trait, TraitName, Requires;
            public readonly DecisionKind Axis, Second;
            public readonly int Amount, SecondAmount, Faction, Feeling;
            public readonly Skills.IDs Skill;
            public readonly Func<UniqueActors, UniqueActor> Unique;
            public Experience(string kind, string id, string name, string trait, string traitName,
                string requires, DecisionKind axis, int amount, DecisionKind second, int secondAmount,
                Skills.IDs skill, int faction = -1, int feeling = 0,
                Func<UniqueActors, UniqueActor> unique = null)
            {
                Kind = kind; Id = id; Name = name; Trait = trait; TraitName = traitName;
                Requires = requires; Axis = axis; Amount = amount; Second = second;
                SecondAmount = secondAmount; Skill = skill; Faction = faction;
                Feeling = feeling; Unique = unique;
            }
        }
        internal sealed class FactionSource
        {
            public readonly GameFactions.IDs Id;
            public readonly string Key, Name;
            public FactionSource(GameFactions.IDs id, string key, string name)
            { Id = id; Key = key; Name = name; }
        }
        static Experience U(string id, string name, string trait, string label, string requires,
            DecisionKind axis, int amount, DecisionKind second, int secondAmount, Skills.IDs skill,
            int feeling, Func<UniqueActors, UniqueActor> unique)
        {
            return new Experience("met_unique:" + id, "met_" + id, "Encountered " + name,
                trait, label, requires, axis, amount, second, secondAmount, skill,
                feeling: feeling, unique: unique);
        }
        static Experience W(string kind, string name, string trait, string label, string requires,
            DecisionKind axis, int amount, DecisionKind second, int secondAmount, Skills.IDs skill,
            GameFactions.IDs faction, int feeling)
        {
            return new Experience(kind, kind, name, trait, label, requires, axis, amount,
                second, secondAmount, skill, (int)faction, feeling);
        }
        public static readonly Experience[] Uniques = {
            U("big_bear", "Big Bear", "bear_resolve", "Bear's resolve", "brave", DecisionKind.Courage, 22, DecisionKind.Supplies, 8, Skills.IDs.TOUGH, 5, u => u.BigBear),
            U("famu_fataru", "Famu Fataru", "blade_discipline", "Blade discipline", "disciplined", DecisionKind.Courage, 15, DecisionKind.Supplies, 15, Skills.IDs.MARTIAL_ARTS, 5, u => u.FamuFataru),
            U("santaman", "Santaman", "holiday_spirit", "Holiday spirit", "generous", DecisionKind.Compassion, 25, DecisionKind.Trade, 15, Skills.IDs.CHARISMATIC, 10, u => u.Santaman),
            U("roguedjack", "Roguedjack", "rogue_ingenuity", "Rogue ingenuity", "curious", DecisionKind.Explore, 20, DecisionKind.Item, 15, Skills.IDs.HAULER, 5, u => u.Roguedjack),
            U("duckman", "Duckman", "duck_camaraderie", "Duck camaraderie", "sociable", DecisionKind.Group, 25, DecisionKind.Courage, 10, Skills.IDs.LEADERSHIP, 5, u => u.Duckman),
            U("hans_von_hanz", "Hans von Hanz", "hans_drill", "Hans's drill", "disciplined", DecisionKind.Courage, 20, DecisionKind.Group, 20, Skills.IDs.FIREARMS, 5, u => u.HansVonHanz),
            U("prisoner", "The Prisoner Who Should Not Be", "prisoner_secrets", "Prisoner's secrets", "suspicious", DecisionKind.Explore, 15, DecisionKind.Law, -20, Skills.IDs.UNSUSPICIOUS, -5, u => u.PoliceStationPrisoner),
            U("jason_myers", "Jason Myers", "masked_survivor", "Masked killer's survivor", "cautious", DecisionKind.Courage, -10, DecisionKind.Supplies, 20, Skills.IDs.TOUGH, -25, u => u.JasonMyers),
            U("sewers_thing", "The Sewers Thing", "sewer_dread", "Sewer dread", "fearful", DecisionKind.Explore, -25, DecisionKind.Courage, -20, Skills.IDs.STRONG_PSYCHE, -25, u => u.TheSewersThing)
        };
        public static readonly FactionSource[] Factions = {
            new FactionSource(GameFactions.IDs.TheCHARCorporation, "char", "CHAR Corp."),
            new FactionSource(GameFactions.IDs.TheCivilians, "civilians", "Civilians"),
            new FactionSource(GameFactions.IDs.TheUndeads, "undead", "Undeads"),
            new FactionSource(GameFactions.IDs.TheArmy, "army", "Army"),
            new FactionSource(GameFactions.IDs.TheBikers, "bikers", "Bikers"),
            new FactionSource(GameFactions.IDs.TheGangstas, "gangstas", "Gangstas"),
            new FactionSource(GameFactions.IDs.ThePolice, "police", "Police"),
            new FactionSource(GameFactions.IDs.TheBlackOps, "blackops", "BlackOps"),
            new FactionSource(GameFactions.IDs.ThePsychopaths, "psychopaths", "Psychopaths"),
            new FactionSource(GameFactions.IDs.TheSurvivors, "survivors", "Survivors"),
            new FactionSource(GameFactions.IDs.TheFerals, "ferals", "Ferals")
        };
        public static readonly Experience[] WorldEvents = {
            W("zombie_invasion", "Midnight invasion", "night_watch", "Night watch", "vigilant", DecisionKind.Courage, 10, DecisionKind.Supplies, 25, Skills.IDs.FIREARMS, GameFactions.IDs.TheUndeads, -15),
            W("sewers_invasion", "Sewers overrun", "underground_caution", "Underground caution", "cautious", DecisionKind.Explore, -20, DecisionKind.Courage, -10, Skills.IDs.STRONG_PSYCHE, GameFactions.IDs.TheUndeads, -15),
            W("refugees_arrival", "Refugees arrived", "refugee_solidarity", "Refugee solidarity", "kind", DecisionKind.Compassion, 25, DecisionKind.Group, 10, Skills.IDs.MEDIC, GameFactions.IDs.TheCivilians, 10),
            W("national_guard_arrival", "National Guard arrived", "army_confidence", "Army confidence", "trusting", DecisionKind.Courage, 15, DecisionKind.Group, 15, Skills.IDs.FIREARMS, GameFactions.IDs.TheArmy, 10),
            W("army_supplies", "Army relief drop", "relief_organizer", "Relief organizer", "organized", DecisionKind.Supplies, 25, DecisionKind.Compassion, 10, Skills.IDs.HAULER, GameFactions.IDs.TheArmy, 15),
            W("bikers_raid", "Biker raid", "roadside_vigilance", "Roadside vigilance", "vigilant", DecisionKind.Supplies, 20, DecisionKind.Courage, 10, Skills.IDs.TOUGH, GameFactions.IDs.TheBikers, -20),
            W("hells_souls_raid", "Hell's Souls raid", "hells_souls_defiance", "Hell's Souls defiance", "brave", DecisionKind.Courage, 20, DecisionKind.Law, 10, Skills.IDs.TOUGH, GameFactions.IDs.TheBikers, -20),
            W("free_angels_raid", "Free Angels raid", "free_angels_watchfulness", "Free Angels watchfulness", "vigilant", DecisionKind.Supplies, 20, DecisionKind.Explore, -10, Skills.IDs.TOUGH, GameFactions.IDs.TheBikers, -20),
            W("gangstas_raid", "Street gang raid", "streetwise", "Streetwise", "pragmatic", DecisionKind.Explore, 15, DecisionKind.Supplies, 15, Skills.IDs.UNSUSPICIOUS, GameFactions.IDs.TheGangstas, -20),
            W("craps_raid", "Craps raid", "craps_grudge", "Craps grudge", "vindictive", DecisionKind.Courage, 20, DecisionKind.Law, -10, Skills.IDs.FIREARMS, GameFactions.IDs.TheGangstas, -20),
            W("floods_raid", "Floods raid", "floods_caution", "Floods caution", "cautious", DecisionKind.Courage, -15, DecisionKind.Supplies, 20, Skills.IDs.UNSUSPICIOUS, GameFactions.IDs.TheGangstas, -20),
            W("blackops_raid", "BlackOps operation", "blackops_distrust", "BlackOps distrust", "suspicious", DecisionKind.Group, -15, DecisionKind.Explore, -15, Skills.IDs.STRONG_PSYCHE, GameFactions.IDs.TheBlackOps, -20),
            W("survivors_arrival", "Survivor convoy", "convoy_hope", "Convoy hope", "sociable", DecisionKind.Group, 25, DecisionKind.Trade, 10, Skills.IDs.CHARISMATIC, GameFactions.IDs.TheSurvivors, 10),
            W("char_discovered", "CHAR facility uncovered", "char_whistleblower", "CHAR whistleblower", "skeptic", DecisionKind.Explore, 20, DecisionKind.Law, 10, Skills.IDs.UNSUSPICIOUS, GameFactions.IDs.TheCHARCorporation, -15),
            W("prisoner_transformed", "Prisoner's transformation", "betrayal_scar", "Betrayal scar", "suspicious", DecisionKind.Group, -25, DecisionKind.Courage, -10, Skills.IDs.STRONG_PSYCHE, GameFactions.IDs.TheCHARCorporation, -15)
        };

        public static void Register(PersonalityRegistry registry)
        {
            foreach (Experience source in Uniques) RegisterExperience(registry, source);
            foreach (Experience source in WorldEvents) RegisterExperience(registry, source);
            foreach (FactionSource source in Factions)
            {
                // Civilian personal aid/violence already has generic memories; refugee events
                // supply the civilian collective experience. Animals and undead cannot give aid.
                if (source.Id == GameFactions.IDs.TheCivilians) continue;
                RegisterFaction(registry, source, false);
                if (source.Id != GameFactions.IDs.TheUndeads && source.Id != GameFactions.IDs.TheFerals)
                {
                    RegisterFaction(registry, source, true);
                    registry.Conflict("friend_" + source.Key, "wary_" + source.Key);
                }
            }
        }
        static void RegisterExperience(PersonalityRegistry registry, Experience source)
        {
            registry.Register(new TraitDefinition(source.Trait, source.TraitName, true, false,
                source.Requires, new TraitEffect(source.Axis, source.Amount),
                new TraitEffect(source.Second, source.SecondAmount)).TowardFaction(source.Faction,
                Math.Sign(source.Feeling) * 10));
            MemoryDefinition memory = new MemoryDefinition(source.Id, source.Name, 2, 6,
                new[] { new MemoryTrigger(source.Kind, (a, e) => a != e.Subject &&
                    (!source.Kind.EndsWith("_raid") || a.Faction == null || a.Faction.ID != source.Faction)) },
                new MemoryOutcome(null, source.Trait, null), new MemoryOutcome(null, null, source.Skill),
                new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(source.Kind == "army_supplies" || source.Kind == "char_discovered"
                    ? MemoryRelationRole.None : source.Kind == "prisoner_transformed"
                    ? MemoryRelationRole.Other : MemoryRelationRole.Subject, source.Feeling);
            if (source.Faction >= 0) memory.TowardFaction(source.Faction, source.Feeling / 2);
            if (source.Unique != null) memory.FirstEncounter();
            registry.Register(memory, false);
        }
        static void RegisterFaction(PersonalityRegistry registry, FactionSource source, bool help)
        {
            int faction = (int)source.Id;
            string trait = (help ? "friend_" : "wary_") + source.Key;
            registry.Register(new TraitDefinition(trait, (help ? "Friend of " : "Wary of ") + source.Name,
                true, false, help ? "trusting" : "suspicious",
                new TraitEffect(help ? DecisionKind.Trade : DecisionKind.Supplies, help ? 10 : 15),
                new TraitEffect(DecisionKind.Group, help ? 10 : -10)).TowardFaction(faction, help ? 15 : -20));
            Func<Actor, SignificantEvent, bool> matches = (a, e) => e.Other != null &&
                e.Other != a && e.Other.Faction != null && e.Other.Faction.ID == faction;
            registry.Register(new MemoryDefinition((help ? "aid_" : "violence_") + source.Key,
                (help ? "Aid from " : "Violence by ") + source.Name, 2, 6,
                help ? new[] { new MemoryTrigger("helped", (a,e) => a == e.Subject && matches(a,e)) }
                : new[] { new MemoryTrigger("attack", (a,e) => a == e.Subject && matches(a,e)),
                    new MemoryTrigger("murder", (a,e) => a != e.Subject && matches(a,e)) },
                new MemoryOutcome(null, trait, null),
                new MemoryOutcome(null, null, help ? Skills.IDs.CHARISMATIC : Skills.IDs.STRONG_PSYCHE),
                new MemoryOutcome(null, null, Skills.IDs.UNSUSPICIOUS))
                .Relate(MemoryRelationRole.Other, 0).TowardFaction(faction, help ? 10 : -15), false);
        }
        public static string EventName(string kind)
        {
            foreach (Experience source in Uniques) if (source.Kind == kind) return source.Name;
            foreach (Experience source in WorldEvents) if (source.Kind == kind) return source.Name;
            return null;
        }
    }
}
