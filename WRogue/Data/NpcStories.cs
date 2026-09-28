using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Data
{
    [Serializable]
    sealed class NpcMapComparer : IEqualityComparer<Map>
    {
        public bool Equals(Map first, Map second) { return Object.ReferenceEquals(first, second); }
        public int GetHashCode(Map map) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(map); }
    }
    [Serializable]
    sealed class NpcMapOffer
    {
        public Map Map;
        public int NextTurn;
    }
    [Serializable]
    sealed class NpcStoryRole
    {
        public Guid ActorId;
        public Guid TargetId;
        public string Name, Goal, Outcome;
        public NpcIntentStatus Status;
    }
    [Serializable]
    sealed class NpcStory
    {
        public string Id, Template, Stage;
        public long CauseId;
        public int StartedTurn, Deadline, FinishedTurn;
        public Location Place, Resource;
        public Guid ReservedActor;
        public readonly List<NpcStoryRole> Roles = new List<NpcStoryRole>();
        public bool Finished { get { return Stage == "completed" || Stage == "failed" || Stage == "abandoned"; } }
    }
    [Serializable]
    sealed class NpcStoryDirector
    {
        public readonly List<NpcStory> Stories = new List<NpcStory>();
        readonly Dictionary<string, int> cooldowns = new Dictionary<string, int>();
        readonly List<NpcMapOffer> nextMapOffer = new List<NpcMapOffer>();
        [NonSerialized] Dictionary<Map, NpcMapOffer> offerIndex;
        public NpcStory Find(string id) { lock (this) return Stories.Find(s => s.Id == id); }
        public bool CanOpen(Map map, int turn, string key, Location resource, Guid reservedActor)
        {
            lock (this)
            {
                int until;
                if (cooldowns.TryGetValue(key, out until) && turn < until) return false;
                int local = 0, total = 0;
                foreach (NpcStory story in Stories)
                {
                    if (story.Finished) continue;
                    total++; if (story.Place.Map == map) local++;
                    if (reservedActor != Guid.Empty && story.ReservedActor == reservedActor) return false;
                    if (resource.Map != null && story.Resource == resource) return false;
                }
                return local < 4 && total < 16;
            }
        }
        public NpcStory Open(string id, string template, Actor owner, long cause, int deadline,
            string key, Location resource = default(Location), Guid reservedActor = default(Guid))
        {
            lock (this)
            {
                NpcStory old = Find(id); if (old != null) return old.Finished ? null : old;
                int turn = owner.Location.Map.LocalTime.TurnCounter;
                if (!CanOpen(owner.Location.Map, turn, key, resource, reservedActor)) return null;
                var story = new NpcStory { Id = id, Template = template, Stage = "seeking", CauseId = cause,
                    StartedTurn = turn, Deadline = deadline, Place = owner.Location, Resource = resource, ReservedActor = reservedActor };
                Stories.Add(story); cooldowns[key] = turn + 180;
                if (cooldowns.Count > 128) RemoveOldestCooldown();
                while (Stories.Count > 64)
                { int index = Stories.FindIndex(s => s.Finished); if (index < 0) break; Stories.RemoveAt(index); }
                return story;
            }
        }
        void RemoveOldestCooldown()
        {
            string key = null; int time = Int32.MaxValue;
            foreach (var pair in cooldowns) if (pair.Value < time) { key = pair.Key; time = pair.Value; }
            if (key != null) cooldowns.Remove(key);
        }
        public void Bind(NpcStory story, Actor actor, NpcIntent intent)
        {
            lock (this)
            {
                NpcStoryRole role = story.Roles.Find(r => r.ActorId == actor.PersonalityIdentity && r.Goal == intent.DefinitionId);
                if (role == null && story.Roles.Count < 8)
                    story.Roles.Add(new NpcStoryRole { ActorId = actor.PersonalityIdentity, Name = actor.UnmodifiedName,
                        Goal = intent.DefinitionId, TargetId = intent.TargetId, Status = intent.Status });
            }
        }
        public void Outcome(Actor actor, NpcIntent intent)
        {
            lock (this)
            {
                NpcStory story = Find(intent.StoryId); if (story == null) return;
                NpcStoryRole role = story.Roles.Find(r => r.ActorId == actor.PersonalityIdentity && r.Goal == intent.DefinitionId);
                if (role != null) { role.Status = intent.Status; role.Outcome = intent.Outcome; }
                if (story.Finished) return;
                if (story.Roles.Count > 0 && story.Roles.TrueForAll(r => r.Status == NpcIntentStatus.Completed))
                { End(story, "completed", intent.FinishedTurn); Session.Get.ResidentRecords.StoryChanged(actor, story, intent.FinishedTurn, 0); return; }
                if (intent.Status == NpcIntentStatus.Completed && story.Template == NpcIntentContentIdRequest && intent.DefinitionId == NpcIntentContentIdRequest)
                { End(story, "completed", intent.FinishedTurn); Session.Get.ResidentRecords.StoryChanged(actor, story, intent.FinishedTurn, 0); return; }
                if (intent.Status != NpcIntentStatus.Completed && story.Roles.TrueForAll(r => r.Status >= NpcIntentStatus.Completed))
                { End(story, "failed", intent.FinishedTurn); Session.Get.ResidentRecords.StoryChanged(actor, story, intent.FinishedTurn, 0); }
            }
        }
        const string NpcIntentContentIdRequest = "request_food";
        public void End(NpcStory story, string stage, int turn)
        { lock (this) { story.Stage = stage; story.FinishedTurn = turn; story.Place = default(Location);
            story.Resource = default(Location); story.ReservedActor = Guid.Empty; } }
        public bool Reserve(string id, Location resource)
        {
            lock (this)
            {
                NpcStory story = Find(id); if (story == null || story.Finished) return false;
                if (resource.Map != null && Stories.Exists(s => s != story && !s.Finished && s.Resource == resource)) return false;
                story.Resource = resource; return true;
            }
        }
        public bool OfferDue(Map map, int turn)
        {
            lock (this)
            {
                if (offerIndex == null)
                { offerIndex = new Dictionary<Map, NpcMapOffer>(new NpcMapComparer()); foreach (NpcMapOffer row in nextMapOffer) offerIndex[row.Map] = row; }
                NpcMapOffer offer;
                if (!offerIndex.TryGetValue(map, out offer)) { offer = new NpcMapOffer { Map = map }; nextMapOffer.Add(offer); offerIndex.Add(map, offer); }
                if (turn < offer.NextTurn) return false;
                offer.NextTurn = turn + 15; return true;
            }
        }
        public void Advance(Map map, int turn)
        {
            lock (this)
                foreach (NpcStory story in Stories)
                    if (!story.Finished && story.Place.Map == map && turn >= story.Deadline) End(story, "failed", turn);
        }
    }
}
