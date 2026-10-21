using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // A subject is chosen from the author's knowledge. Voice is composed from
    // an actual starting trait, faction and remembered experience.
    static class NpcNoteTemplates
    {
        public static NoteTopic TopicFor(Actor author, NpcFact fact)
        {
            if (fact == null || fact.Place.Map == null) return 0;
            if (fact.Kind == "cache_loss_discovered" && fact.SubjectId == author.PersonalityIdentity)
                return NoteTopic.RequestReturn;
            if (fact.Kind == "cache_theft" || fact.Kind == "base_theft")
            {
                if (fact.SubjectId == author.PersonalityIdentity && fact.Source == NpcKnowledgeSource.Participant)
                {
                    if (author.Personality == null) return NoteTopic.Confession;
                    if (author.Personality.HasTrait("kind") || author.Personality.HasTrait("honest") ||
                        author.Personality.HasTrait("forgiving")) return NoteTopic.Regret;
                    if (author.Personality.HasTrait("fearful") || author.Personality.HasTrait("timid"))
                        return NoteTopic.Plea;
                    if (author.Personality.HasTrait("cruel") || author.Personality.HasTrait("vindictive"))
                        return NoteTopic.Taunt;
                    if (author.Personality.HasTrait("hotheaded") || author.Personality.HasTrait("rebellious"))
                        return NoteTopic.Threat;
                    if (author.Personality.HasTrait("pragmatic") || author.Personality.HasTrait("opportunist") ||
                        author.Personality.HasTrait("selfish")) return NoteTopic.Justification;
                    return NoteTopic.Confession;
                }
                return NoteTopic.Warning;
            }
            return fact.Kind == "attack" || fact.Kind == "helped" || fact.Kind == "death" ?
                NoteTopic.Event : 0;
        }

        // Every spawnable trait has its own opening. Combining this with the
        // faction line and an actual memory yields the template variants.
        static readonly Dictionary<string, string> TraitOpeners = new Dictionary<string, string> {
            { "kind", "Please let this help someone. " },
            { "cruel", "Make of this what you will. " },
            { "lawful", "I want this on record. " },
            { "rebellious", "I will say it plainly. " },
            { "brave", "I am not afraid to say this. " },
            { "timid", "I hope this stays in the right hands. " },
            { "sociable", "Pass this along. " },
            { "solitary", "I rarely tell anyone this. " },
            { "loyal", "Those close to me should know. " },
            { "independent", "This is my own account. " },
            { "generous", "I hope someone can use this. " },
            { "selfish", "I have my reasons for writing. " },
            { "frugal", "I cannot afford to forget this. " },
            { "impulsive", "I am writing while it is fresh. " },
            { "patient", "I have thought this through. " },
            { "vigilant", "Watch closely. " },
            { "careless", "I nearly let this slip my mind. " },
            { "curious", "I want someone to look into this. " },
            { "cautious", "Treat this carefully. " },
            { "ambitious", "This may matter later. " },
            { "humble", "This is only what I know. " },
            { "honest", "Here is the truth as I know it. " },
            { "deceptive", "You can decide what to believe. " },
            { "trusting", "I trust someone will read this. " },
            { "suspicious", "I do not know who will read this. " },
            { "protective", "Keep others safe. " },
            { "vindictive", "Do not forget what happened. " },
            { "forgiving", "There may still be a way to settle this. " },
            { "organized", "I am recording the details. " },
            { "messy", "I had better write this down now. " },
            { "hoarder", "I keep what may matter. " },
            { "minimalist", "Only the essential details follow. " },
            { "healer", "I hope nobody is hurt by this. " },
            { "scavenger", "This may prove useful. " },
            { "homebody", "I think of home when I write this. " },
            { "wanderer", "I may not be here when you read this. " },
            { "peacemaker", "I hope this can be settled peacefully. " },
            { "hotheaded", "I have had enough. " },
            { "pragmatic", "These are the useful facts. " },
            { "idealist", "I still hope things can improve. " },
            { "thrillseeker", "This caught my attention. " },
            { "fearful", "I am worried about what comes next. " },
            { "disciplined", "I am keeping a clear account. " },
            { "stubborn", "I stand by this account. " },
            { "adaptable", "Plans may change, but this happened. " },
            { "devout", "I pray this reaches someone. " },
            { "skeptic", "Check this for yourself if you can. " },
            { "opportunist", "This information may be worth something. " },
            { "likes_items", "I pay attention to what people keep. " },
            { "dislikes_items", "I would rather travel light. " }
        };

        internal static bool HasTraitTemplate(string id) { return TraitOpeners.ContainsKey(id); }

        static string Place(Location place)
        { return place.Map.Name + " (" + place.Position.X + "," + place.Position.Y + ")"; }

        static string EvidenceLead(NpcFact evidence)
        {
            return evidence.Source == NpcKnowledgeSource.Witness ? "I saw that " :
                evidence.Source == NpcKnowledgeSource.Participant ? "I know that " :
                evidence.Source == NpcKnowledgeSource.Told ? "I heard that " :
                "I inferred that ";
        }

        static string Voice(Actor author, NpcFact evidence)
        {
            if (author.Personality == null) return "";
            var choices = new List<string>();
            foreach (TraitInstance trait in author.Personality.Traits)
            {
                string opener;
                if (TraitOpeners.TryGetValue(trait.Id, out opener)) choices.Add(opener);
            }
            if (choices.Count == 0) return "";
            long seed = evidence == null ? author.Location.Map.LocalTime.TurnCounter : evidence.EventId;
            return choices[(int)(Math.Abs(seed) % choices.Count)];
        }

        static string Faction(Actor author)
        {
            if (author.Faction == null) return "To whoever finds this,";
            foreach (PersonalityWorldContent.FactionSource source in PersonalityWorldContent.Factions)
                if ((int)source.Id == author.Faction.ID)
                    return source.Key == "army" || source.Key == "police" || source.Key == "blackops" ?
                        "To my colleagues in the " + source.Name + "," :
                        source.Key == "bikers" || source.Key == "gangstas" ? "To the " + source.Name + " crew," :
                        source.Key == "civilians" || source.Key == "survivors" ? "To the " + source.Name + " community," :
                        "To anyone in " + source.Name + ",";
            return "To whoever finds this,";
        }

        static string Memory(Actor author)
        {
            if (author.Personality == null) return "";
            MemoryInstance recent = null;
            foreach (MemoryInstance memory in author.Personality.Memories)
                if (recent == null || memory.StartTurn > recent.StartTurn) recent = memory;
            if (recent == null) return "";
            MemoryDefinition definition = PersonalitySystem.Registry.Memory(recent.Id);
            if (definition == null) return "";
            if (recent.Id == "heard_a_report")
                return String.IsNullOrEmpty(recent.Subject) ?
                    "Someone told me what happened, and I have not forgotten it." :
                    recent.Subject + " told me what happened, and I have not forgotten it.";
            if (recent.Id == "received_help")
                return String.IsNullOrEmpty(recent.Subject) ?
                    "Someone helped me once, and I have not forgotten it." :
                    recent.Subject + " helped me once, and I have not forgotten it.";
            return String.IsNullOrEmpty(recent.Subject) ?
                "I have not forgotten this part of my life: " + definition.Name.ToLowerInvariant() + "." :
                "I have not forgotten " + recent.Subject + ".";
        }

        public static string Render(Actor author, NoteTopic topic, Location place,
            string resource, int units, NpcFact evidence)
        {
            string where = Place(place);
            string subject = evidence != null && evidence.NamesSubject ? evidence.ReportSubject : "someone";
            string what = evidence == null ? resource : evidence.Resource ?? resource;
            int count = evidence == null ? units : evidence.Units;
            string detail;
            switch (topic)
            {
                case NoteTopic.Location: detail = "I was at " + where + "."; break;
                case NoteTopic.CacheSupplies:
                    detail = "I saw " + units + " " + resource + " in a cache at " + where + "."; break;
                case NoteTopic.RequestReturn:
                    detail = "I found " + count + " " + (what ?? "supplies") +
                        " missing from my cache at " + where + ". Please return what was taken."; break;
                case NoteTopic.Warning:
                    detail = EvidenceLead(evidence) +
                        subject + " took supplies at " + where + ". Be careful."; break;
                case NoteTopic.SearchIntent:
                    detail = "I intend to look for supplies at " + where + "."; break;
                case NoteTopic.Confession:
                    detail = "I took " + count + " " + (what ?? "items") + " from the cache at " + where + "."; break;
                case NoteTopic.Regret:
                    detail = "I took " + count + " " + (what ?? "items") +
                        " from the cache at " + where + ". I regret it."; break;
                case NoteTopic.Taunt:
                    detail = "I took " + count + " " + (what ?? "items") +
                        " from the cache at " + where + ". You did not stop me."; break;
                case NoteTopic.Justification:
                    detail = "I took " + count + " " + (what ?? "items") +
                        " from the cache at " + where + ". I thought taking it was worth the risk."; break;
                case NoteTopic.Plea:
                    detail = "I took " + count + " " + (what ?? "items") +
                        " from the cache at " + where + ". Please let me go."; break;
                case NoteTopic.Threat:
                    detail = "I took " + count + " " + (what ?? "items") +
                        " from the cache at " + where + ". Follow me and you will regret it."; break;
                default:
                    detail = evidence.Kind == "cache_loss_discovered" ?
                        "I found supplies missing from a cache at " + where + "." :
                        evidence.Kind == "cache_theft" || evidence.Kind == "base_theft" ?
                        EvidenceLead(evidence) +
                        subject + " took supplies at " + where + "." :
                        "I learned of " + evidence.Kind.Replace('_', ' ') + " at " + where + ".";
                    break;
            }
            string memoryText = Memory(author);
            string opening = Voice(author, evidence).Trim();
            return Faction(author) + "\n\n" +
                (memoryText.Length == 0 ? "" : memoryText + "\n\n") +
                (opening.Length == 0 ? "" : opening + " ") + detail;
        }
    }
}
