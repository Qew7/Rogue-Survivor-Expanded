using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    static partial class RecordsReader
    {
        // Resolve only evidence retained in the archive; no world or live NPC state is consulted.
        static Dictionary<long, ResidentEntry> CauseIndex(RecordsSave save)
        {
            var index = new Dictionary<long, ResidentEntry>();
            foreach (ResidentRecord resident in save.Records.Residents)
                foreach (ResidentEntry entry in resident.Entries)
                {
                    if (entry.EventId <= 0 || entry.Turn > save.Turn || entry.Kind == "story_stage") continue;
                    ResidentEntry previous;
                    if (!index.TryGetValue(entry.EventId, out previous) || entry.Direct && !previous.Direct)
                        index[entry.EventId] = entry;
                }
            return index;
        }

        static string CauseContext(ResidentEntry entry, Dictionary<long, ResidentEntry> index)
        {
            if (entry.CauseId <= 0 && (entry.SupportingCauses == null || entry.SupportingCauses.Length == 0)) return null;
            var ids = new List<long>(3);
            if (entry.CauseId > 0) ids.Add(entry.CauseId);
            if (entry.SupportingCauses != null) foreach (long id in entry.SupportingCauses)
                if (id > 0 && !ids.Contains(id)) ids.Add(id);
            var reasons = new List<string>(2);
            foreach (long id in ids)
            {
                ResidentEntry cause;
                if (id == entry.EventId || !index.TryGetValue(id, out cause) || cause == entry || cause.Turn > entry.Turn) continue;
                string detail = cause.Text;
                if (detail.StartsWith("Experienced: ", StringComparison.Ordinal)) detail = detail.Substring(13);
                else if (detail.StartsWith("Witnessed: ", StringComparison.Ordinal)) detail = detail.Substring(11);
                int story = detail.LastIndexOf(" [story ", StringComparison.Ordinal);
                if (story >= 0) detail = detail.Substring(0, story);
                detail = detail.Trim().TrimEnd('.');
                if (detail.Length == 0 || reasons.Contains(detail)) continue;
                if (detail.Length > 150) detail = detail.Substring(0, 147).TrimEnd() + "...";
                reasons.Add(detail);
                if (reasons.Count == 2) break;
            }
            if (reasons.Count == 0) return null;
            return (entry.Kind == "goal_started" ? "Prompted by: " : "Connected to: ") + String.Join("; ", reasons.ToArray()) + ".";
        }
    }
}
