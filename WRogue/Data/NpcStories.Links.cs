using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Data
{
    sealed partial class NpcStoryDirector
    {
        public Guid ReservationOwner(Location place, string exceptStory)
        {
            lock (this)
            {
                NpcStory story = Stories.Find(s => !s.Finished && s.Id != exceptStory && s.Resource == place);
                return story == null ? Guid.Empty : story.ReservedActor != Guid.Empty ? story.ReservedActor : story.Roles.Count == 0 ? Guid.Empty : story.Roles[0].ActorId;
            }
        }
        public void ReleaseOwned(Location place, Guid owner)
        {
            lock (this)
                foreach (NpcStory story in Stories)
                    if (!story.Finished && story.Resource == place && story.Roles.Exists(r => r.ActorId == owner)) story.Resource = default(Location);
        }
        public void Link(NpcStory child, string parent, Actor owner, long cause)
        {
            if (parent == null || parent == child.Id || child.Parents.Contains(parent) || Ancestor(parent, child.Id)) return;
            if (child.Parents.Count < 8) child.Parents.Add(parent);
            Session.Get.ResidentRecords.LinkStory(owner, parent, child.Id, cause);
        }
        bool Ancestor(string from, string target)
        {
            var seen = new HashSet<string>(); var pending = new Queue<string>(); pending.Enqueue(from);
            while (pending.Count > 0 && seen.Count <= 64)
            {
                string id = pending.Dequeue(); if (id == target) return true; if (!seen.Add(id)) continue;
                NpcStory story = Find(id); if (story != null) foreach (string parent in story.Parents) pending.Enqueue(parent);
            }
            return false;
        }
    }
}
