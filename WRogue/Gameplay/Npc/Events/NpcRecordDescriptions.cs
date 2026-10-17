using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcRecordDescriptions
    {
        public const int Classified = 1 << 16;
        public static int Categories(string kind)
        {
            if (kind == "memory" || kind == "resolved" || kind == "initial-trait") return Classified | (int)NpcRecordCategory.Memories;
            if (kind.StartsWith("goal_", StringComparison.Ordinal) || kind == "story_stage" || kind == "knowledge_inferred" || kind == "story_link") return Classified | (int)NpcRecordCategory.Intentions;
            NpcEventDefinition definition = NpcContentCatalog.Default.Event(kind);
            return Classified | (definition == null ? 0 : (int)definition.Categories);
        }
        public static string Describe(ObservedEvent e)
        {
            NpcEventDefinition definition = NpcContentCatalog.Default.Event(e.Kind);
            if (definition != null && definition.Describe != null) return definition.Describe(e);
            return e.Kind + ": " + (e.Subject ?? "Someone") + (e.Other == null ? "." : "; " + e.Other + ".");
        }
        public static void Annotate(ObservedEvent observed, NpcEventDefinition definition, NpcContentCatalog catalog)
        {
            if (definition == null) return;
            observed.RecordCategories = Classified | (int)definition.Categories;
            if (!Object.ReferenceEquals(catalog, NpcContentCatalog.Default) && definition.Describe != null) observed.RecordText = definition.Describe(observed);
        }
        public static bool Matches(ResidentEntry entry, NpcRecordCategory category)
        { return ((entry.RecordCategories == 0 ? Categories(entry.Kind) : entry.RecordCategories) & (int)category) != 0; }
        public static string Report(NpcContentCatalog catalog, NpcFact fact)
        {
            NpcEventDefinition definition = catalog.Event(fact.Kind);
            if (definition != null && definition.DescribeReport != null) return definition.DescribeReport(fact);
            if (definition != null && definition.Describe != null)
            {
                string report = definition.Describe(new ObservedEvent(fact.Kind, fact.EventTurn,
                    fact.ReportSubject, fact.ReportOther, false, subjectId: fact.SubjectId, otherId: fact.OtherId));
                if (!String.IsNullOrEmpty(report))
                    return report.EndsWith(".", StringComparison.Ordinal) ? report.Substring(0, report.Length - 1) : report;
            }
            string participants = fact.ReportSubject == null ? fact.ReportOther :
                fact.ReportOther == null ? fact.ReportSubject : fact.ReportSubject + " and " + fact.ReportOther;
            return "there was " + fact.Kind.Replace('_', ' ') + (participants == null ? "" : " involving " + participants);
        }

        public static string RequestGroup(IList<NpcFact> facts, string resource)
        {
            var names = new List<string>();
            foreach (NpcFact fact in facts) names.Add(fact.ReportOther);
            string recipients = names.Count == 2 ? names[0] + " and " + names[1] :
                String.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + ", and " + names[names.Count - 1];
            return facts[0].ReportSubject + " asked " + recipients + " for " + resource;
        }
    }
}
